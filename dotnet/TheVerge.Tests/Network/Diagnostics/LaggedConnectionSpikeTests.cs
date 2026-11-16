using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Diagnostics;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Network.Diagnostics
{
    /// <summary>
    /// Всплеск лага до 500 мс (PLAN Стадия 3, п. 12; ADR 0020 п. 5): входящие пакеты держатся до конца всплеска и
    /// приходят пачкой в один тик без обгона, а показ чужих мобов (<see cref="OthersMotion"/>) догоняет её ускоренно и без
    /// лишних перескоков — даже при беге верхом (100 мс/шаг).
    /// </summary>
    public sealed class LaggedConnectionSpikeTests
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
            _lag = new LagSimulation();
            _pool = new CountingArrayPool();
            _connection = new LaggedConnection(_inner, _clock, _lag, new StepRandom(0), _pool);
        }

        [TearDown]
        public void TearDown() => _connection.Dispose();

        [Test]
        public void Spike500Ms_BaseLagOff_HoldsAndReleasesBurstInOrder()
        {
            _lag.TriggerIncomingSpike(500);
            for (byte i = 1; i <= 5; i++)
            {
                _clock.NowMs = 1000 + (i - 1) * 100;
                _inner.Push(Packet(0x77, i));
                Assert.That(_connection.TryReceive(out _), Is.False, "во время всплеска пакеты держатся");
            }

            _clock.NowMs = 1499;
            Assert.That(_connection.TryReceive(out _), Is.False);

            _clock.NowMs = 1500;
            var got = ReceiveAllIds();
            Assert.That(got, Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));

            _clock.NowMs = 1550;
            _inner.Push(Packet(0x77, 6));
            Assert.That(ReceiveAllIds(), Is.EqualTo(new byte[] { 6 }), "после всплеска — снова без задержки");
        }

        [Test]
        public void Spike500Ms_MountedOtherMobile_CatchesUpWithZeroSnaps()
        {
            var log = new RecordingLog();
            var dispatcher = new PacketDispatcher(log);
            var store = new EntityStore();
            store.Reset(new PlayerState(new Serial(0x01), 0x0190, new Point3D(100, 100, 0), Direction.East, new MapSize(512, 512)));
            store.ChangeMap(0);
            var others = new OthersMotion(store, _clock);
            WorldHandlers.Register(dispatcher, store, new WorldEnvironment(), null, others);
            var session = new NetworkSession(_connection, dispatcher, log);
            var client = new GameClient(session, _clock, others);

            // Появление чужого верхом на коне (слой Mount 0x19) и один начальный шаг.
            PushIncomingRider(0x00000100, 110, 100);
            _clock.NowMs = 1000;
            client.Tick(1000);
            PushStep(0x00000100, 111, 100, Direction.East | Direction.Running);
            _clock.NowMs = 1100;
            client.Tick(1100);

            // Всплеск 500 мс прямо во время бега верхом (5 шагов каждые 100 мс).
            _lag.TriggerIncomingSpike(500);
            for (short x = 112; x <= 116; x++)
            {
                _clock.NowMs += 100;
                PushStep(0x00000100, x, 100, Direction.East | Direction.Running);
                client.Tick(_clock.NowMs);
            }

            for (int t = 0; t < 20; t++)
            {
                _clock.NowMs += 50;
                client.Tick(_clock.NowMs);
            }

            Assert.That(others.SnapCount, Is.EqualTo(0), "всплеск 500 мс проходит без лишних перескоков");
            Assert.That(others.CatchUpCount, Is.GreaterThan(0), "пачка шагов догоняется ускоренно");
            Assert.That(store.FindMobile(new Serial(0x00000100))!.Position, Is.EqualTo(new Point3D(116, 100, 0)));
            others.Dispose();
        }

        private List<byte> ReceiveAllIds()
        {
            var result = new List<byte>();
            while (_connection.TryReceive(out var item))
            {
                result.Add(item.Packet[1]);
                item.Release();
            }

            return result;
        }

        private InboundEvent Packet(params byte[] bytes)
        {
            var buffer = _pool.Rent(bytes.Length);
            bytes.CopyTo(buffer, 0);
            return InboundEvent.FromPacket(buffer, bytes.Length, _pool);
        }

        // 0x78 MobileIncoming: 23 байта заголовка + 9 байт надетого коня (слой 0x19) + 4 нуля конца.
        private void PushIncomingRider(uint serial, short x, short y)
        {
            var bytes = new byte[23 + 9 + 4];
            bytes[0] = 0x78;
            bytes[2] = (byte)bytes.Length;
            WriteU32(bytes, 3, serial);
            WriteU16(bytes, 7, 0x0190);
            WriteU16(bytes, 9, (ushort)x);
            WriteU16(bytes, 11, (ushort)y);
            bytes[13] = 0;
            bytes[14] = (byte)Direction.East;
            bytes[18] = 1; // notoriety
            WriteU32(bytes, 19, serial + 1); // конь
            WriteU16(bytes, 23, 0x3EA2);
            bytes[25] = (byte)Layer.Mount;
            _inner.Push(Packet(bytes));
        }

        // 0x77 MobileMoving (17 байт).
        private void PushStep(uint serial, short x, short y, Direction dir)
        {
            var bytes = new byte[17];
            bytes[0] = 0x77;
            WriteU32(bytes, 1, serial);
            WriteU16(bytes, 5, 0x0190);
            WriteU16(bytes, 7, (ushort)x);
            WriteU16(bytes, 9, (ushort)y);
            bytes[11] = 0;
            bytes[12] = (byte)dir;
            bytes[16] = 1;
            _inner.Push(Packet(bytes));
        }

        private static void WriteU16(byte[] dst, int offset, ushort value)
        {
            dst[offset] = (byte)(value >> 8);
            dst[offset + 1] = (byte)value;
        }

        private static void WriteU32(byte[] dst, int offset, uint value)
        {
            dst[offset] = (byte)(value >> 24);
            dst[offset + 1] = (byte)(value >> 16);
            dst[offset + 2] = (byte)(value >> 8);
            dst[offset + 3] = (byte)value;
        }
    }
}