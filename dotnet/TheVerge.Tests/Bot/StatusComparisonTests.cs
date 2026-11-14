using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.World;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Сверка состояния мобов бота с дампом сервера [DumpMobile (PLAN Стадия 4, п. 4; формат — Custom/TheVerge/Parity/
    /// MobileStatusDump.cs сервера). Свой персонаж — все числа полного статуса и замки; чужой — имя и здоровье долей, как
    /// сервер шлёт чужим (cur × 100 / max и 100, OutgoingMobilePackets.WriteAttribute), яд и жёлтая полоска.
    /// </summary>
    public sealed class StatusComparisonTests
    {
        private const uint Player = 0x3E;
        private const uint Rat = 0x100;

        private EntityStore _store = null!;
        private MobileStatusUpdates _updates = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _store.Reset(new PlayerState(new Serial(Player), 0x190, new Point3D(1, 1, 0), Direction.North, new MapSize(7168, 4096)));
            _store.UpdateMobile(new Serial(Rat), new MobileInfo(0xEE, new Point3D(2, 2, 0), Direction.North, 0, 0, 3));
            _updates = new MobileStatusUpdates(_store);
            var sheet = new StatusSheet(false, new Vital(35, 40), new Vital(20, 21), new StatusAttributes(35, 36, 20, 1000, 3, 79, 225), 0, 5);
            _updates.OnFullStatus(new Serial(Player), "Борис", new Vital(60, 67), false, 3, sheet);
            _updates.OnStatLocks(new Serial(Player), new StatLocks(StatLock.Locked, StatLock.Down, StatLock.Up));
            _updates.OnCompactStatus(new Serial(Rat), "a rat", new Vital(66, 100), false);
            _updates.OnHealthbar(new Serial(Rat), 1, 2);
        }

        [Test]
        public void SameState_NoDifferences()
        {
            var report = Compare(Dump());

            Assert.That(report.Differences, Is.Empty, string.Join("\n", report.Differences));
            Assert.That(string.Join("\n", report.Summary), Does.Contain("мобов 2"));
        }

        [TestCase("str", 50, "сила")]
        [TestCase("gold", 7, "золото")]
        [TestCase("stamMax", 41, "выносливость")]
        [TestCase("strLock", 0, "замок силы")]
        public void SelfFieldDiffers_Named(string field, int value, string name)
        {
            var dump = Dump();
            Self(dump)[field] = value;

            Assert.That(Compare(dump).Differences, Has.Exactly(1).Contains("свой персонаж").And.Contains(name));
        }

        [Test]
        public void OtherHits_ComparedAsShareOf100()
        {
            var dump = Dump();
            var rat = Other(dump);
            rat["hits"] = 5;
            rat["hitsMax"] = 7; // 5 × 100 / 7 = 71, у клиента 66

            Assert.That(Compare(dump).Differences, Has.Exactly(1).Contains("0x00000100").And.Contains("здоровье"));
        }

        [Test]
        public void OtherWithoutStatus_Named()
        {
            _store.UpdateMobile(new Serial(0x200), new MobileInfo(0xEE, new Point3D(3, 3, 0), Direction.North, 0, 0, 3));
            var dump = Dump();
            ((JsonArray)dump["mobiles"]!).Add(Entry(0x200, false, "a rat", 10, 10));

            Assert.That(Compare(dump).Differences, Has.Exactly(1).Contains("0x00000200").And.Contains("статуса"));
        }

        [Test]
        public void PoisonAndMissingMobile_Named()
        {
            var dump = Dump();
            Other(dump)["poison"] = 0;
            ((JsonArray)dump["mobiles"]!).Add(Entry(0x300, false, "ghost", 1, 1));

            var differences = Compare(dump).Differences;

            Assert.That(differences, Has.Some.Contains("0x00000100").And.Some.Contains("яд"));
            Assert.That(differences, Has.Some.Contains("0x00000300").And.Some.Contains("у клиента нет"));
        }

        private ParityReport Compare(JsonObject dump)
        {
            using (var document = JsonDocument.Parse(dump.ToJsonString()))
            {
                return StatusComparison.Compare(document.RootElement, _store);
            }
        }

        private static JsonObject Dump()
        {
            var self = Entry(Player, true, "Борис", 60, 67);
            self["mana"] = 20;
            self["manaMax"] = 21;
            self["stam"] = 35;
            self["stamMax"] = 40;
            self["str"] = 35;
            self["dex"] = 36;
            self["int"] = 20;
            self["gold"] = 1000;
            self["armor"] = 3;
            self["weight"] = 79;
            self["statCap"] = 225;
            self["followersMax"] = 5;
            self["strLock"] = 2;
            self["dexLock"] = 1;
            var rat = Entry(Rat, false, "a rat", 2, 3); // 2 × 100 / 3 = 66
            rat["poison"] = 2;
            return new JsonObject { ["player"] = "0x0000003E", ["mobiles"] = new JsonArray(self, rat) };
        }

        private static JsonObject Entry(uint serial, bool self, string name, int hits, int hitsMax) => new JsonObject
        {
            ["serial"] = "0x" + serial.ToString("X8"), ["self"] = self, ["name"] = name, ["hits"] = hits, ["hitsMax"] = hitsMax,
            ["mana"] = 0, ["manaMax"] = 0, ["stam"] = 0, ["stamMax"] = 0, ["female"] = false, ["str"] = 0, ["dex"] = 0, ["int"] = 0,
            ["gold"] = 0, ["armor"] = 0, ["weight"] = 0, ["statCap"] = 0, ["followers"] = 0, ["followersMax"] = 0, ["poison"] = 0,
            ["yellow"] = false, ["strLock"] = 0, ["dexLock"] = 0, ["intLock"] = 0,
        };

        private static JsonObject Self(JsonObject dump) => (JsonObject)dump["mobiles"]![0]!;

        private static JsonObject Other(JsonObject dump) => (JsonObject)dump["mobiles"]![1]!;
    }
}
