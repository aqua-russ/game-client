using System;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;
using static TheVerge.Tests.Network.Packets.World.ServerWorldPackets;

namespace TheVerge.Tests.Network.Packets.World
{
    /// <summary>
    /// Пакеты мобов (PLAN Стадия 2, п. 9): 0x78, 0x20, 0x77, 0x2E, 0x1D — байты по коду сервера (ServerWorldPackets) и
    /// настоящие байты из записанного входа бота (Bot/Replays/bot-login-modernuo.tsv).
    /// </summary>
    public sealed class MobilePacketTests
    {
        private const uint Player = 0x3E;
        private const uint Orc = 0x100;

        private EntityStore _store = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _dispatcher = new PacketDispatcher(new RecordingLog());
            WorldHandlers.Register(_dispatcher, _store);
            _store.Reset(new PlayerState(new Serial(Player), 0x190, new Point3D(633, 858, 0), Direction.North, new MapSize(7168, 4096)));
        }

        [Test]
        public void MobileIncoming_RecordedFromServer_PlayerWithSevenItems()
        {
            // 0x78 своего персонажа из записанного входа на ModernUO (7 надетых предметов, без волос).
            var packet = Hex("78 00 56 00 00 00 3E 01 90 02 79 03 5A 00 00 83 EA 00 01 40 00 01 E2 0E 75 15 00 00 40 00 01 F5 " +
                             "0E FA 01 00 00 40 00 01 F6 1F 03 16 05 1B 40 00 01 F7 17 18 06 00 00 40 00 01 F9 15 17 05 00 02 " +
                             "40 00 01 FA 15 39 04 00 02 40 00 01 FB 17 0F 03 06 D2 00 00 00 00");

            Assert.That(_dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.Handled));

            var player = _store.Player!;
            Assert.That(player.Body, Is.EqualTo((ushort)0x190));
            Assert.That(player.Position, Is.EqualTo(new Point3D(633, 858, 0)));
            Assert.That(player.Hue, Is.EqualTo((ushort)0x83EA));
            Assert.That(player.Notoriety, Is.EqualTo((byte)1));
            Assert.That(player.EquippedCount, Is.EqualTo(7));
            var backpack = player.GetEquipped(Layer.Backpack)!;
            Assert.That(backpack.Serial.Value, Is.EqualTo(0x400001E2u));
            Assert.That(backpack.Graphic, Is.EqualTo((ushort)0x0E75));
            Assert.That(player.GetEquipped(Layer.OuterTorso)!.Hue, Is.EqualTo((ushort)0x051B));
            Assert.That(player.GetEquipped(Layer.Shoes)!.Hue, Is.EqualTo((ushort)0x06D2));
        }

        [Test]
        public void MobileIncoming_AllFields()
        {
            var packet = MobileIncoming(new Mob { Serial = Orc, Body = 0x0011, X = 640, Y = 860, Z = -7, Direction = 0x84, Hue = 0x0455, Flags = 0x40, Notoriety = 6 },
                new Equipped(0x40000100, 0x13B9, 0x01, 0x0000),
                new Equipped(0x7FFFFF00, 0x203B, 0x0B, 0x044E));

            _dispatcher.Dispatch(packet);

            var orc = _store.FindMobile(new Serial(Orc))!;
            Assert.That(orc.Body, Is.EqualTo((ushort)0x0011));
            Assert.That(orc.Position, Is.EqualTo(new Point3D(640, 860, -7)));
            Assert.That(orc.Direction, Is.EqualTo(Direction.South | Direction.Running));
            Assert.That(orc.Hue, Is.EqualTo((ushort)0x0455));
            Assert.That(orc.Flags, Is.EqualTo((byte)0x40));
            Assert.That(orc.Notoriety, Is.EqualTo((byte)6));
            Assert.That(orc.GetEquipped(Layer.OneHanded)!.Graphic, Is.EqualTo((ushort)0x13B9));
            var hair = orc.GetEquipped(Layer.Hair)!;
            Assert.That(hair.Serial.Value, Is.EqualTo(0x7FFFFF00u), "волосы — виртуальный предмет сервера (World.NewVirtual)");
            Assert.That(hair.Hue, Is.EqualTo((ushort)0x044E));
        }

        [Test]
        public void MobileIncoming_Repeated_ReplacesEquipment()
        {
            _dispatcher.Dispatch(MobileIncoming(new Mob { Serial = Orc, Body = 0x11, X = 1, Y = 1, Notoriety = 3 },
                new Equipped(0x40000100, 0x13B9, 0x01, 0), new Equipped(0x40000101, 0x1517, 0x05, 0)));

            _dispatcher.Dispatch(MobileIncoming(new Mob { Serial = Orc, Body = 0x11, X = 1, Y = 1, Notoriety = 3 }, new Equipped(0x40000101, 0x1517, 0x05, 0)));

            var orc = _store.FindMobile(new Serial(Orc))!;
            Assert.That(orc.EquippedCount, Is.EqualTo(1));
            Assert.That(_store.FindItem(new Serial(0x40000100)), Is.Null);
        }

        [TestCase("лишний байт после конца списка", 1)]
        [TestCase("нет конца списка", -4)]
        [TestCase("обрезан предмет", -5)]
        public void MobileIncoming_LengthDoesNotMatchItems_SkippedWorldUnchanged(string what, int delta)
        {
            var good = MobileIncoming(new Mob { Serial = Orc, Body = 0x11, X = 1, Y = 1, Notoriety = 3 }, new Equipped(0x40000100, 0x13B9, 0x01, 0));
            var bad = Resize(good, good.Length + delta);

            Assert.That(_dispatcher.Dispatch(bad), Is.EqualTo(DispatchResult.ReadError), what);
            Assert.That(_store.FindMobile(new Serial(Orc)), Is.Null);
            Assert.That(_store.FindItem(new Serial(0x40000100)), Is.Null);
        }

        [Test]
        public void MobileIncoming_ZeroSerialInsideList_Skipped()
        {
            // Ноль — конец списка (сервер пишет int 0 последним): ноль в середине значит, что длина не сходится.
            var packet = MobileIncoming(new Mob { Serial = Orc, Body = 0x11, X = 1, Y = 1, Notoriety = 3 },
                new Equipped(0, 0x13B9, 0x01, 0), new Equipped(0x40000101, 0x1517, 0x05, 0));

            Assert.That(_dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.ReadError));
            Assert.That(_store.FindMobile(new Serial(Orc)), Is.Null);
        }

        [Test]
        public void MobileUpdate_RecordedFromServer_PlayerUpdatedNotorietyKept()
        {
            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Player, Body = 0x190, X = 633, Y = 858, Notoriety = 1 }));

            _dispatcher.Dispatch(Hex("20 00 00 00 3E 01 90 00 83 EA 00 02 79 03 5A 00 00 00 00"));

            var player = _store.Player!;
            Assert.That(player.Position, Is.EqualTo(new Point3D(633, 858, 0)));
            Assert.That(player.Hue, Is.EqualTo((ushort)0x83EA));
            Assert.That(player.Notoriety, Is.EqualTo((byte)1), "в 0x20 «злости» нет");
        }

        [Test]
        public void MobileUpdate_AllFields()
        {
            _dispatcher.Dispatch(MobileUpdate(new Mob { Serial = Player, Body = 0x191, X = 1500, Y = 1600, Z = -12, Direction = 0x07, Hue = 0x0400, Flags = 0x02, Notoriety = 1 }));

            var player = _store.Player!;
            Assert.That(player.Body, Is.EqualTo((ushort)0x191));
            Assert.That(player.Hue, Is.EqualTo((ushort)0x0400));
            Assert.That(player.Flags, Is.EqualTo((byte)0x02));
            Assert.That(player.Position, Is.EqualTo(new Point3D(1500, 1600, -12)));
            Assert.That(player.Direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        public void MobileMoving_Unknown_StubCounted_Known_Moved()
        {
            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Orc, Body = 0x11, X = 640, Y = 860, Z = 3, Direction = 0x02, Hue = 0x455, Flags = 0x80, Notoriety = 5 }));
            Assert.That(_store.Metrics.UnknownMobileMoves, Is.EqualTo(1));

            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Orc, Body = 0x11, X = 641, Y = 860, Z = 4, Direction = 0x02, Hue = 0x455, Flags = 0x80, Notoriety = 5 }));

            var orc = _store.FindMobile(new Serial(Orc))!;
            Assert.That(orc.Position, Is.EqualTo(new Point3D(641, 860, 4)));
            Assert.That(orc.Direction, Is.EqualTo(Direction.East));
            Assert.That(orc.Flags, Is.EqualTo((byte)0x80));
            Assert.That(orc.Notoriety, Is.EqualTo((byte)5));
            Assert.That(_store.Metrics.UnknownMobileMoves, Is.EqualTo(1));
        }

        [Test]
        public void EquipUpdate_HairOnPlayer()
        {
            _dispatcher.Dispatch(EquipUpdate(0x7FFFFF10, 0x203C, 0x0B, Player, 0x0455));

            var hair = _store.Player!.GetEquipped(Layer.Hair)!;
            Assert.That(hair.Serial.Value, Is.EqualTo(0x7FFFFF10u));
            Assert.That(hair.Graphic, Is.EqualTo((ushort)0x203C));
            Assert.That(hair.Hue, Is.EqualTo((ushort)0x0455));
        }

        [Test]
        public void EquipUpdate_UnknownMobile_Rejected()
        {
            _dispatcher.Dispatch(EquipUpdate(0x40000300, 0x1517, 0x05, Orc, 0));

            Assert.That(_store.FindItem(new Serial(0x40000300)), Is.Null);
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void RemoveEntity_KnownAndUnknown()
        {
            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Orc, Body = 0x11, X = 640, Y = 860, Z = 3, Direction = 0, Hue = 0, Flags = 0, Notoriety = 5 }));

            _dispatcher.Dispatch(RemoveEntity(Orc));
            _dispatcher.Dispatch(RemoveEntity(0x40009999));

            Assert.That(_store.FindMobile(new Serial(Orc)), Is.Null);
            Assert.That(_store.Metrics.UnknownRemoves, Is.EqualTo(1));
        }

        [Test]
        public void SteadyStream_NoAllocations()
        {
            var moving = MobileMoving(new Mob { Serial = Orc, Body = 0x11, X = 640, Y = 860, Z = 3, Direction = 0, Hue = 0, Flags = 0, Notoriety = 5 });
            var incoming = MobileIncoming(new Mob { Serial = Orc, Body = 0x11, X = 640, Y = 860, Notoriety = 3 }, new Equipped(0x40000100, 0x13B9, 0x01, 0));
            _dispatcher.Dispatch(incoming);
            _dispatcher.Dispatch(moving);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                _dispatcher.Dispatch(incoming);
                _dispatcher.Dispatch(moving);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private static byte[] Resize(byte[] packet, int length)
        {
            var copy = new byte[length];
            Array.Copy(packet, copy, Math.Min(length, packet.Length));
            copy[1] = (byte)(length >> 8);
            copy[2] = (byte)length;
            return copy;
        }

        private static byte[] Hex(string text) => text.Split(' ').Select(b => Convert.ToByte(b, 16)).ToArray();
    }
}
