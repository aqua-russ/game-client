using System;
using NUnit.Framework;
using TheVerge.Core.Diagnostics;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Diagnostics;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Network.Diagnostics
{
    /// <summary>
    /// Симуляция пинга (PLAN Стадия 3, п. 9; ADR 0020 п. 9): соединение-обёртка задерживает события от сервера и отправку
    /// клиента по настройкам <see cref="LagSimulation"/>, не меняя ни байта и не переставляя пакеты. Выключено — всё
    /// проходит в тот же вызов. Отложенная отправка уходит при следующем обращении к соединению (GameClient каждый тик
    /// спрашивает новые события). Подходит и боту (задача 3.12).
    /// </summary>
    public sealed class LaggedConnectionTests
    {
        private FakeConnection _inner = null!;
        private FakeClock _clock = null!;
        private LagSimulation _lag = null!;
        private CountingArrayPool _pool = null!;
        private LaggedConnection _connection = null!;

        [SetUp]
        public void SetUp()
        {
            _inner = new FakeConnection();
            _clock = new FakeClock { NowMs = 1000 };
            _lag = new LagSimulation { IncomingJitterMs = 0, OutgoingJitterMs = 0 };
            _pool = new CountingArrayPool();
            _connection = new LaggedConnection(_inner, _clock, _lag, new StepRandom(0), _pool);
        }

        [TearDown]
        public void TearDown() => _connection.Dispose();

        [Test]
        public void Off_PacketsAndSendsPassAtOnce_SameBytes()
        {
            _inner.Push(Packet(0x73, 1));

            Assert.That(_connection.TryReceive(out var item), Is.True);
            Assert.That(item.Packet.ToArray(), Is.EqualTo(new byte[] { 0x73, 1 }));
            item.Release();
            Assert.That(_connection.Send(new byte[] { 0x02, 3, 4 }), Is.True);
            Assert.That(_inner.Sent, Has.Count.EqualTo(1));
            Assert.That(_inner.Sent[0], Is.EqualTo(new byte[] { 0x02, 3, 4 }));
        }

        [Test]
        public void IncomingDelay_EventComesOutAfterDelay_Unchanged()
        {
            Enable(incoming: 100, outgoing: 0);
            _inner.Push(Packet(0x22, 5, 0));

            Assert.That(_connection.TryReceive(out _), Is.False);
            _clock.NowMs = 1099;
            Assert.That(_connection.TryReceive(out _), Is.False);
            _clock.NowMs = 1100;
            Assert.That(_connection.TryReceive(out var item), Is.True);
            Assert.That(item.Kind, Is.EqualTo(InboundKind.Packet));
            Assert.That(item.Packet.ToArray(), Is.EqualTo(new byte[] { 0x22, 5, 0 }));
            item.Release();
        }

        [Test]
        public void OutgoingDelay_SentAfterDelay_OnNextCall_Unchanged()
        {
            Enable(incoming: 0, outgoing: 100);

            Assert.That(_connection.Send(new byte[] { 0x02, 0x80, 7 }), Is.True);
            Assert.That(_inner.Sent, Is.Empty);
            _clock.NowMs = 1099;
            _connection.TryReceive(out _);
            Assert.That(_inner.Sent, Is.Empty);
            _clock.NowMs = 1100;
            _connection.TryReceive(out _);

            Assert.That(_inner.Sent, Has.Count.EqualTo(1));
            Assert.That(_inner.Sent[0], Is.EqualTo(new byte[] { 0x02, 0x80, 7 }));
            Assert.That(_pool.Outstanding, Is.Zero, "копия отправки вернулась в пул");
        }

        [Test]
        public void Jitter_PacketOrderKept_BothWays()
        {
            // Настоящие случайные задержки: своё соединение поверх нового (прежнее закрыто вместе с обёрткой).
            _connection.Dispose();
            _inner = new FakeConnection();
            _connection = new LaggedConnection(_inner, _clock, _lag, new Random(7), _pool);
            _lag.Enabled = true;
            _lag.IncomingJitterMs = 30;
            _lag.OutgoingJitterMs = 30;
            var received = new System.Collections.Generic.List<byte>();
            for (byte i = 0; i < 50; i++)
            {
                _clock.NowMs = 1000 + i * 5;
                _inner.Push(Packet(0x77, i));
                _connection.Send(new[] { (byte)0x02, i });
                ReceiveAll(received);
            }

            for (long now = 1250; now <= 1500; now += 10)
            {
                _clock.NowMs = now;
                ReceiveAll(received);
            }

            Assert.That(received, Is.EqualTo(Sequence(50)), "входящие — в порядке сервера");
            Assert.That(_inner.Sent.ConvertAll(s => s[1]), Is.EqualTo(Sequence(50)), "исходящие — в порядке клиента");
        }

        [Test]
        public void TurnedOffWhileQueued_QueuedFirst_ThenNewAtOnce()
        {
            Enable(incoming: 200, outgoing: 200);
            _inner.Push(Packet(0x77, 1));
            _connection.TryReceive(out _);
            _connection.Send(new byte[] { 0x02, 1 });

            _lag.Enabled = false;
            _clock.NowMs = 1050;
            _inner.Push(Packet(0x77, 2));
            _connection.Send(new byte[] { 0x02, 2 });

            Assert.That(_connection.TryReceive(out _), Is.False, "новый ждёт старый");
            Assert.That(_inner.Sent, Is.Empty);
            _clock.NowMs = 1200;
            Assert.That(_connection.TryReceive(out var first), Is.True);
            Assert.That(_connection.TryReceive(out var second), Is.True);
            Assert.That((first.Packet[1], second.Packet[1]), Is.EqualTo(((byte)1, (byte)2)));
            Assert.That(_inner.Sent.ConvertAll(s => s[1]), Is.EqualTo(new byte[] { 1, 2 }));
            first.Release();
            second.Release();

            _clock.NowMs = 1300;
            _inner.Push(Packet(0x77, 3));
            Assert.That(_connection.TryReceive(out var third), Is.True, "очередь пуста — снова насквозь");
            third.Release();
        }

        [Test]
        public void ConnectedAndClosed_DelayedInOrderWithPackets()
        {
            Enable(incoming: 100, outgoing: 0);
            _inner.Push(InboundEvent.Connected());
            _inner.Push(Packet(0xA9, 1));
            _inner.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));

            Assert.That(_connection.TryReceive(out _), Is.False);
            _clock.NowMs = 1100;
            Assert.That(_connection.TryReceive(out var a), Is.True);
            Assert.That(_connection.TryReceive(out var b), Is.True);
            Assert.That(_connection.TryReceive(out var c), Is.True);
            Assert.That((a.Kind, b.Kind, c.Kind), Is.EqualTo((InboundKind.Connected, InboundKind.Packet, InboundKind.Closed)));
            b.Release();
        }

        [Test]
        public void NotConnected_SendRefused()
        {
            Enable(incoming: 0, outgoing: 100);
            _inner.State = ConnectionState.Closed;

            Assert.That(_connection.Send(new byte[] { 0x02 }), Is.False);
            Assert.That(_pool.Outstanding, Is.Zero);
        }

        [Test]
        public void ConnectAndCompression_GoStraightToInner()
        {
            _connection.Connect("127.0.0.1", 2593, default);
            _connection.EnableCompression();

            Assert.That(_inner.ConnectedTo, Is.EqualTo("127.0.0.1:2593"));
            Assert.That(_inner.CompressionEnabled, Is.True);
            Assert.That(_connection.State, Is.EqualTo(ConnectionState.Connecting));
        }

        [Test]
        public void Dispose_ReturnsHeldBuffers_ClosesInner()
        {
            Enable(incoming: 500, outgoing: 500);
            _inner.Push(Packet(0x77, 1));
            _connection.TryReceive(out _);
            _connection.Send(new byte[] { 0x02, 1 });
            Assert.That(_pool.Outstanding, Is.EqualTo(2));

            _connection.Dispose();

            Assert.That(_pool.Outstanding, Is.Zero, "ни один буфер не потерян");
            Assert.That(_inner.Disposed, Is.True);
            Assert.That(_inner.Sent, Is.Empty, "задержанная отправка после закрытия не уходит");
        }

        private void ReceiveAll(System.Collections.Generic.List<byte> received)
        {
            while (_connection.TryReceive(out var item))
            {
                received.Add(item.Packet[1]);
                item.Release();
            }
        }

        private void Enable(int incoming, int outgoing)
        {
            _lag.Enabled = true;
            _lag.IncomingDelayMs = incoming;
            _lag.OutgoingDelayMs = outgoing;
        }

        private InboundEvent Packet(params byte[] bytes)
        {
            var buffer = _pool.Rent(bytes.Length);
            bytes.CopyTo(buffer, 0);
            return InboundEvent.FromPacket(buffer, bytes.Length, _pool);
        }

        private static byte[] Sequence(int count)
        {
            var result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = (byte)i;
            }

            return result;
        }
    }
}
