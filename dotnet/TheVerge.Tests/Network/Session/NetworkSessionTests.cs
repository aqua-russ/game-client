using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Framing;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>
    /// Сеть глазами ядра (порт INetworkSession, ADR 0013 п. 7): по одному событию из очереди транспорта — пакет
    /// разбирается по номеру и его буфер возвращается в пул; ошибка нарезки — дисконнект с дампом в лог
    /// (AGENTS.md §3.5); закрытие — запись в лог. Исходящие тика уходят одной отправкой (AGENTS.md §2.3).
    /// </summary>
    public sealed class NetworkSessionTests
    {
        private CountingArrayPool _pool = new CountingArrayPool();
        private FakeConnection _connection = new FakeConnection();
        private RecordingLog _log = new RecordingLog();

        // NUnit создаёт один экземпляр класса на все тесты: каждому тесту — своё соединение, лог и пул.
        [SetUp]
        public void SetUp()
        {
            _pool = new CountingArrayPool();
            _connection = new FakeConnection();
            _log = new RecordingLog();
        }

        [Test]
        public void EmptyQueue_ProcessNextReturnsFalse()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);

            Assert.That(session.ProcessNext(), Is.False);
        }

        [Test]
        public void Packets_HandledInOrderOfArrival_BuffersReturned()
        {
            var order = new List<byte>();
            var dispatcher = new PacketDispatcher(_log);
            dispatcher.Register(0x55, (ref PacketReader r) => order.Add(0x55));
            dispatcher.Register(0x73, (ref PacketReader r) => order.Add(0x73));
            var session = new NetworkSession(_connection, dispatcher, _log);
            Push(ServerPacketSamples.PingAck());
            Push(ServerPacketSamples.LoginComplete());
            Push(ServerPacketSamples.PingAck());

            while (session.ProcessNext())
            {
            }

            Assert.That(order, Is.EqualTo(new byte[] { 0x73, 0x55, 0x73 }));
            Assert.That(_pool.Outstanding, Is.EqualTo(0));
        }

        [Test]
        public void ReadError_BufferStillReturned_NextPacketHandled()
        {
            var handled = 0;
            var dispatcher = new PacketDispatcher(_log);
            dispatcher.Register(0x73, (ref PacketReader r) => r.ReadUInt32());
            dispatcher.Register(0x55, (ref PacketReader r) => handled++);
            var session = new NetworkSession(_connection, dispatcher, _log);
            Push(ServerPacketSamples.PingAck());
            Push(ServerPacketSamples.LoginComplete());

            session.ProcessNext();
            session.ProcessNext();

            Assert.That(handled, Is.EqualTo(1));
            Assert.That(_pool.Outstanding, Is.EqualTo(0));
            Assert.That(_connection.Disposed, Is.False);
        }

        [Test]
        public void FramingFailed_DisconnectWithDumpInLog()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);
            var error = new FramingError(FramingErrorKind.UnknownPacket, 0x02, 0, 0, new byte[] { 0x02, 0x01 });
            _connection.Push(InboundEvent.FramingFailed(error));
            Push(ServerPacketSamples.LoginComplete());

            Assert.That(session.ProcessNext(), Is.True);

            Assert.That(_connection.Disposed, Is.True);
            Assert.That(session.IsConnected, Is.False);
            Assert.That(_log.Errors.Single(), Does.Contain("0x02").And.Contain("02 01"));
            Assert.That(session.ProcessNext(), Is.False);
            Assert.That(_pool.Outstanding, Is.EqualTo(0));
        }

        [Test]
        public void Closed_LoggedWithReason_NotConnected()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);
            _connection.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));
            _connection.State = ConnectionState.Closed;

            session.ProcessNext();

            Assert.That(session.IsConnected, Is.False);
            Assert.That(_log.Entries.Any(e => e.Message.Contains(nameof(ConnectionCloseReason.ServerClosed))), Is.True);
        }

        [Test]
        public void Open_NotClosed_NoReason()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);
            _connection.Push(InboundEvent.Connected());

            session.ProcessNext();

            Assert.That(session.IsClosed, Is.False);
            Assert.That(session.CloseReason, Is.EqualTo(ConnectionCloseReason.None));
        }

        [Test]
        public void Closed_ReasonAndDetailKept()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);
            _connection.Push(InboundEvent.Closed(ConnectionCloseReason.ConnectFailed, "Connection refused"));

            session.ProcessNext();

            Assert.That(session.IsClosed, Is.True);
            Assert.That(session.CloseReason, Is.EqualTo(ConnectionCloseReason.ConnectFailed));
            Assert.That(session.CloseDetail, Is.EqualTo("Connection refused"));
        }

        [Test]
        public void FramingFailed_ClosedByFraming()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);
            var error = new FramingError(FramingErrorKind.UnknownPacket, 0x02, 0, 0, new byte[] { 0x02, 0x01 });
            _connection.Push(InboundEvent.FramingFailed(error));

            session.ProcessNext();

            Assert.That(session.IsClosed, Is.True);
            Assert.That(session.CloseReason, Is.EqualTo(ConnectionCloseReason.FramingFailed));
            Assert.That(session.CloseDetail, Does.Contain("0x02"));
        }

        [Test]
        public void Flush_AllPacketsOfTickInOneSend()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);
            Write(session, new byte[] { 0x73, 0x01 });
            Write(session, new byte[] { 0xD1, 0x00 });

            session.Flush();
            session.Flush();

            Assert.That(_connection.Sent.Count, Is.EqualTo(1));
            Assert.That(_connection.Sent[0], Is.EqualTo(new byte[] { 0x73, 0x01, 0xD1, 0x00 }));
        }

        [Test]
        public void Flush_NothingWritten_NoSend()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);

            session.Flush();

            Assert.That(_connection.Sent, Is.Empty);
        }

        [Test]
        public void Flush_ConnectionGone_DroppedAndWarnedOnce()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);
            _connection.AcceptsSend = false;

            Write(session, new byte[] { 0x73, 0x01 });
            session.Flush();
            Write(session, new byte[] { 0x73, 0x02 });
            session.Flush();

            Assert.That(session.Outgoing.Pending.Length, Is.EqualTo(0));
            Assert.That(_log.Warnings.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Dispose_ClosesConnection()
        {
            var session = new NetworkSession(_connection, new PacketDispatcher(_log), _log);

            session.Dispose();

            Assert.That(_connection.Disposed, Is.True);
        }

        private void Push(byte[] packet)
        {
            var buffer = _pool.Rent(packet.Length);
            packet.CopyTo(buffer, 0);
            _connection.Push(InboundEvent.FromPacket(buffer, packet.Length, _pool));
        }

        private static void Write(NetworkSession session, byte[] packet)
        {
            packet.CopyTo(session.Outgoing.Reserve(packet.Length));
            session.Outgoing.Commit(packet.Length);
        }
    }
}
