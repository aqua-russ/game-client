using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.World;
using TheVerge.Network.Packets.Status;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;
using static TheVerge.Tests.Network.Packets.Status.ServerStatusPackets;

namespace TheVerge.Tests.Network.Packets.Status
{
    /// <summary>
    /// Состояние моба (PLAN Стадия 4, п. 4): 0x11 (свой — версия 3, чужой — краткий: здоровье долей от 100), 0xA1–0xA3,
    /// 0x2D, 0x17, 0xBF 0x19 — байты по коду сервера (ServerStatusPackets) и настоящие из записанного входа бота.
    /// Кому что шлёт сервер — Mobile.ProcessDelta: своему — точные числа, чужим — 0xA1 и краткий 0x11 долей.
    /// </summary>
    public sealed class StatusPacketTests
    {
        private const uint Player = 0x3E;
        private const uint Orc = 0x100;

        private EntityStore _store = null!;
        private MobileStatusUpdates _updates = null!;
        private PacketDispatcher _dispatcher = null!;
        private List<uint> _changed = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _store.Reset(new PlayerState(new Serial(Player), 0x190, new Point3D(1, 1, 0), Direction.North, new MapSize(7168, 4096)));
            _store.UpdateMobile(new Serial(Orc), new MobileInfo(0x11, new Point3D(2, 2, 0), Direction.North, 0, 0, 3));
            _updates = new MobileStatusUpdates(_store);
            _changed = new List<uint>();
            _updates.Changed += m => _changed.Add(m.Serial.Value);
            _dispatcher = new PacketDispatcher(new RecordingLog());
            StatusHandlers.Register(_dispatcher, _updates);
        }

        [Test]
        public void Full_RecordedFromServer()
        {
            // 0x11 своего персонажа «Replay» из записанного входа на ModernUO (Bot/Replays/bot-login-modernuo.tsv).
            var packet = Hex("11 00 46 00 00 00 3E 52 65 70 6C 61 79 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
                             "00 00 00 00 00 00 43 00 43 00 03 00 00 23 00 23 00 14 00 23 00 23 00 14 00 14 00 00 03 E8 00 00 " +
                             "00 4F 00 E1 00 05");

            Assert.That(_dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.Handled));

            var s = Status(Player);
            Assert.That(s.Name, Is.EqualTo("Replay"));
            Assert.That(s.Version, Is.EqualTo(3));
            Assert.That((s.Hits.Current, s.Hits.Maximum), Is.EqualTo((67, 67)));
            Assert.That((s.Attributes.Str, s.Attributes.Dex, s.Attributes.Int), Is.EqualTo((35, 35, 20)));
            Assert.That((s.Stam.Current, s.Stam.Maximum, s.Mana.Current, s.Mana.Maximum), Is.EqualTo((35, 35, 20, 20)));
            Assert.That((s.Attributes.Gold, s.Attributes.Armor, s.Attributes.Weight, s.Attributes.StatCap), Is.EqualTo((1000, 0, 79, 225)));
            Assert.That((s.Followers, s.FollowersMax, s.Female, s.CanBeRenamed), Is.EqualTo((0, 5, false, false)));
            Assert.That(_changed, Is.EqualTo(new[] { Player }));
        }

        [Test]
        public void Full_EveryFieldInPlace()
        {
            var sheet = new Sheet
            {
                Hits = 11, HitsMax = 12, Female = true, Str = 13, Dex = 14, Int = 15, Stam = 16, StamMax = 17, Mana = 18,
                ManaMax = 19, Gold = 70000, Armor = 21, Weight = 22, StatCap = 23, Followers = 2, FollowersMax = 4,
            };

            _dispatcher.Dispatch(Full(Player, "Борис", sheet));

            var s = Status(Player);
            Assert.That(s.Name, Is.EqualTo("Борис"));
            Assert.That((s.Hits.Current, s.Hits.Maximum, s.Female), Is.EqualTo((11, 12, true)));
            Assert.That((s.Attributes.Str, s.Attributes.Dex, s.Attributes.Int), Is.EqualTo((13, 14, 15)));
            Assert.That((s.Stam.Current, s.Stam.Maximum, s.Mana.Current, s.Mana.Maximum), Is.EqualTo((16, 17, 18, 19)));
            Assert.That((s.Attributes.Gold, s.Attributes.Armor, s.Attributes.Weight, s.Attributes.StatCap), Is.EqualTo((70000, 21, 22, 23)));
            Assert.That((s.Followers, s.FollowersMax), Is.EqualTo((2, 4)));
        }

        [Test]
        public void Compact_OtherMobile_HitsAsShareOf100()
        {
            _dispatcher.Dispatch(Compact(Orc, "an orc", 30, 120, true));

            var s = Status(Orc);
            Assert.That(s.Name, Is.EqualTo("an orc"));
            Assert.That((s.Hits.Current, s.Hits.Maximum), Is.EqualTo((25, 100)));
            Assert.That(s.Version, Is.EqualTo(0));
            Assert.That(s.CanBeRenamed, Is.True);
            Assert.That(s.Attributes.Str, Is.Zero, "краткий статус статов не несёт");
        }

        [TestCase(60)]
        [TestCase(43)]
        public void Full_LengthNotMatchingVersion_Skipped(int length)
        {
            var packet = Full(Player, "Replay", new Sheet());
            var cut = packet.Take(length).ToArray();
            cut[1] = (byte)(length >> 8);
            cut[2] = (byte)length;

            Assert.That(_dispatcher.Dispatch(cut), Is.EqualTo(DispatchResult.ReadError));
            Assert.That(Status(Player).Name, Is.Empty);
            Assert.That(_changed, Is.Empty);
        }

        [TestCase(0xA1)]
        [TestCase(0xA2)]
        [TestCase(0xA3)]
        public void Vital_MaxThenCurrent(int id)
        {
            _dispatcher.Dispatch(Vital((byte)id, Player, 100, 40));

            var s = Status(Player);
            var vital = id == 0xA1 ? s.Hits : id == 0xA2 ? s.Mana : s.Stam;
            Assert.That((vital.Current, vital.Maximum), Is.EqualTo((40, 100)));
            Assert.That(_changed, Is.EqualTo(new[] { Player }));
        }

        [Test]
        public void Attributes_AllThree()
        {
            _dispatcher.Dispatch(Attributes(Player, (50, 49), (30, 29), (40, 39)));

            var s = Status(Player);
            Assert.That((s.Hits.Current, s.Hits.Maximum, s.Mana.Current, s.Mana.Maximum, s.Stam.Current, s.Stam.Maximum),
                Is.EqualTo((49, 50, 29, 30, 39, 40)));
        }

        [Test]
        public void Healthbar_PoisonAndYellow()
        {
            _dispatcher.Dispatch(Healthbar(Orc, 1, 3));
            _dispatcher.Dispatch(Healthbar(Orc, 2, 1));
            Assert.That((Status(Orc).PoisonLevel, Status(Orc).YellowHealthbar), Is.EqualTo((3, true)));

            _dispatcher.Dispatch(Healthbar(Orc, 1, 0));
            _dispatcher.Dispatch(Healthbar(Orc, 2, 0));
            Assert.That((Status(Orc).PoisonLevel, Status(Orc).YellowHealthbar), Is.EqualTo((0, false)));
        }

        [Test]
        public void Healthbar_RecordedFromServer_GmBlessed()
        {
            // 0x17 GM из записанного входа (Bot/Replays/world-dump-modernuo.tsv): жёлтая полоска уровня 1, яда нет.
            _store.UpdateMobile(new Serial(0x01), new MobileInfo(0x190, new Point3D(3, 3, 0), Direction.North, 0, 0, 1));
            _dispatcher.Dispatch(Hex("17 00 0C 00 00 00 01 00 01 00 02 01"));
            _dispatcher.Dispatch(Hex("17 00 0C 00 00 00 01 00 01 00 01 00"));

            Assert.That((Status(0x01).YellowHealthbar, Status(0x01).PoisonLevel), Is.EqualTo((true, 0)));
        }

        [Test]
        public void ExtendedStats_BondedAndStatLocks()
        {
            _dispatcher.Dispatch(Bonded(Orc, true));
            _dispatcher.Dispatch(StatLocks(Player, 2, 1, 0));

            Assert.That(Status(Orc).IsDeadBonded, Is.True);
            var locks = Status(Player).Locks;
            Assert.That((locks.Str, locks.Dex, locks.Int), Is.EqualTo((StatLock.Locked, StatLock.Down, StatLock.Up)));
        }

        [Test]
        public void ExtendedStats_UnknownCommand_Ignored()
        {
            var packet = Bonded(Orc, true);
            packet[5] = 7;

            Assert.That(_dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.Handled));
            Assert.That(Status(Orc).IsDeadBonded, Is.False);
            Assert.That(_changed, Is.Empty);
        }

        [Test]
        public void UnknownMobile_SkippedAndCounted()
        {
            _dispatcher.Dispatch(Vital(0xA1, 0x999, 10, 5));
            _dispatcher.Dispatch(Compact(0x998, "x", 1, 2, false));

            Assert.That(_store.FindMobile(new Serial(0x999)), Is.Null);
            Assert.That(_updates.UnknownMobile, Is.EqualTo(2));
            Assert.That(_changed, Is.Empty);
        }

        [Test]
        public void VitalStream_NoAllocations()
        {
            var hits = Vital(0xA1, Orc, 100, 50);
            var all = Attributes(Player, (50, 49), (30, 29), (40, 39));
            _dispatcher.Dispatch(hits);
            _dispatcher.Dispatch(all);
            _changed.Capacity = 4000; // список событий самого теста не должен расти во время замера

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                _dispatcher.Dispatch(hits);
                _dispatcher.Dispatch(all);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private MobileStatus Status(uint serial) => _store.FindMobile(new Serial(serial))!.Status;

        private static byte[] Hex(string text) => text.Split(' ').Select(b => Convert.ToByte(b, 16)).ToArray();
    }
}
