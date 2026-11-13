using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using TheVerge.Core.Items;
using TheVerge.Core.Primitives;
using TheVerge.Core.Targeting;
using TheVerge.Core.World;
using TheVerge.Network.Framing;
using TheVerge.Network.Packets.Items;
using TheVerge.Network.Packets.Targeting;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Fuzz
{
    /// <summary>
    /// Устойчивость мира (PLAN Стадия 2, п. 14; критерий «фаззинг новых парсеров: 1 млн входов без падений и зависаний»).
    /// Пакеты мира, контейнеров и прицела — случайные байты и пакеты из небольшого набора серийников (чтобы попадать в уже
    /// известные объекты: вложенность, циклы, перенос, удаление). Падение допустимо одно — «пакет короче, чем нужно»
    /// (PacketDispatcher ловит его сам). После — мир цел: связи взаимны, циклов нет, поиск рядом совпадает с перебором.
    /// Seed — в сообщении: упавший случай повторяется.
    /// </summary>
    public sealed class WorldFuzzTests
    {
        private const int Inputs = 1_000_000;

        private static readonly byte[] Ids = { 0x78, 0x20, 0x77, 0x2E, 0x1D, 0xF3, 0x76, 0x24, 0x3C, 0x25, 0x6C, 0x4F, 0x4E, 0xBC, 0x5B, 0x65, 0x54, 0x6D };

        [Test]
        public void MillionPackets_WorldStaysConsistent()
        {
            var seed = Environment.TickCount;
            var random = new Random(seed);
            var store = new EntityStore();
            var dispatcher = NewDispatcher(store);
            var buffer = new byte[1024];
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < Inputs; i++)
            {
                if (i % 5000 == 0)
                {
                    store.Reset(new PlayerState(Pool.Mobile(random), 0x190, Pool.Cell(random), Direction.North, new MapSize(7168, 4096)));
                }

                var length = random.Next(3) == 0 ? RandomBytes(random, buffer) : WorldPacketBuilder.Build(random, buffer);
                Dispatch(dispatcher, buffer, length, seed, i);
                // Связи — часто: циклы и висячие ссылки живут недолго, следующие пакеты их разрывают; поиск рядом — реже (дорог).
                if (i % 1000 == 999)
                {
                    WorldInvariants.Check(store, random, "seed " + seed + ", вход " + i, i % 100_000 == 99_999);
                }
            }

            WorldInvariants.Check(store, random, "seed " + seed + ", конец");
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(60), "seed " + seed);
        }

        private static PacketDispatcher NewDispatcher(EntityStore store)
        {
            var dispatcher = new PacketDispatcher(new RecordingLog());
            WorldHandlers.Register(dispatcher, store, new WorldEnvironment());
            ItemHandlers.Register(dispatcher, new ContainerContents(store));
            var session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());
            TargetHandlers.Register(dispatcher, new TargetCursor(new TargetSender(session)));
            return dispatcher;
        }

        private static void Dispatch(PacketDispatcher dispatcher, byte[] buffer, int length, int seed, int i)
        {
            try
            {
                dispatcher.Dispatch(buffer.AsSpan(0, length));
            }
            catch (Exception e)
            {
                Assert.Fail("seed " + seed + ", вход " + i + ": " + BitConverter.ToString(buffer, 0, length) + " → " + e);
            }
        }

        // Пакет мира со случайными байтами, который прошёл бы нарезку: длина по таблице или случайная для переменной.
        private static int RandomBytes(Random random, byte[] buffer)
        {
            var id = Ids[random.Next(Ids.Length)];
            var info = PacketTable.Get(PacketDirection.ServerToClient, id);
            var length = info.IsVariable ? random.Next(PacketInfo.VariableHeaderLength, buffer.Length) : info.FixedLength;
            random.NextBytes(buffer.AsSpan(0, length));
            buffer[0] = id;
            if (info.IsVariable)
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(PacketInfo.LengthOffset), (ushort)length);
            }

            return length;
        }

        /// <summary>Небольшие наборы серийников и клеток: случайные пакеты чаще попадают в уже известные объекты.</summary>
        internal static class Pool
        {
            public static Serial Mobile(Random r) => new Serial((uint)r.Next(1, 12));

            public static Serial Item(Random r) => new Serial(0x40000000u + (uint)r.Next(0, 40));

            /// <summary>Любой серийник: свой, чужой вид, 0, за пределами предметов.</summary>
            public static Serial Any(Random r) => r.Next(4) switch
            {
                0 => Mobile(r),
                1 => new Serial((uint)r.Next()),
                2 => r.Next(2) == 0 ? Serial.Zero : new Serial(0x80000000u | (uint)r.Next()),
                _ => Item(r)
            };

            public static Point3D Cell(Random r) => new Point3D((short)r.Next(990, 1030), (short)r.Next(990, 1030), (sbyte)r.Next(-10, 10));
        }
    }
}
