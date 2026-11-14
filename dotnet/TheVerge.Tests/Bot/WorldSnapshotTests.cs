using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Снимок мира бота и сверка с дампом сервера (PLAN Стадия 2, п. 11). Формат дампа — сервер:
    /// Projects/UOContent/Custom/TheVerge/Parity/VisibleObjectsDump.cs (ToJson): player, map, objects[] — serial, kind,
    /// place (world, equipped, container), graphic, hue, amount, x, y, z, parent, layer. Надетое — место 0 и количество 1,
    /// в контейнере — Z = 0: так объект знает клиент.
    /// </summary>
    public sealed class WorldSnapshotTests
    {
        private static readonly Serial Player = new Serial(0x01);

        // Кусок настоящего дампа копии сервера (GM в пустом мире): персонаж, рюкзак, вещь в рюкзаке.
        private const string ServerJson = @"{
  ""player"": ""0x00000001"",
  ""map"": 0,
  ""x"": 5445,
  ""y"": 1153,
  ""z"": 0,
  ""objects"": [
    { ""serial"": ""0x00000001"", ""kind"": ""mobile"", ""place"": ""world"", ""graphic"": 400, ""hue"": 33770, ""amount"": 1,
      ""x"": 5445, ""y"": 1153, ""z"": 0, ""parent"": ""0x00000000"", ""layer"": 0 },
    { ""serial"": ""0x40000001"", ""kind"": ""item"", ""place"": ""equipped"", ""graphic"": 3701, ""hue"": 0, ""amount"": 1,
      ""x"": 0, ""y"": 0, ""z"": 0, ""parent"": ""0x00000001"", ""layer"": 21 },
    { ""serial"": ""0x40000002"", ""kind"": ""item"", ""place"": ""container"", ""graphic"": 4081, ""hue"": 0, ""amount"": 1,
      ""x"": 119, ""y"": 132, ""z"": 0, ""parent"": ""0x40000001"", ""layer"": 0 }
  ]
}";

        [Test]
        public void ReadServerJson_AllFields()
        {
            var dump = WorldSnapshot.Read(ServerJson);

            Assert.That(dump.Player, Is.EqualTo(Player));
            Assert.That(dump.Objects.Count, Is.EqualTo(3));
            var item = dump.Objects[2];
            Assert.That(item.Serial.Value, Is.EqualTo(0x40000002u));
            Assert.That(item.Kind, Is.EqualTo("item"));
            Assert.That(item.Place, Is.EqualTo("container"));
            Assert.That(item.Graphic, Is.EqualTo(4081));
            Assert.That(item.X, Is.EqualTo(119));
            Assert.That(item.Y, Is.EqualTo(132));
            Assert.That(item.Parent.Value, Is.EqualTo(0x40000001u));
        }

        [Test]
        public void FromStore_SameWorld_NoDifferences()
        {
            var store = new EntityStore();
            store.Reset(new PlayerState(Player, 400, new Point3D(5445, 1153, 0), Direction.North, new MapSize(7168, 4096)));
            store.ChangeMap(0); // в дампе сервера map = 0: бот получает карту от сервера (0xBF 0x08), сверка её проверяет
            store.UpdateMobile(Player, new MobileInfo(400, new Point3D(5445, 1153, 0), Direction.North, 33770, 0, 1));
            store.Equip(new Serial(0x40000001), new ItemInfo(3701, 1, Point3D.Zero, 0, 0, 0), Player, Layer.Backpack);
            store.PlaceInContainer(new Serial(0x40000002), new ItemInfo(4081, 1, new Point3D(119, 132, 0), 0, 0, 0), new Serial(0x40000001), 0);

            var differences = WorldSnapshot.Compare(WorldSnapshot.FromStore(store), WorldSnapshot.Read(ServerJson));

            Assert.That(differences, Is.Empty);
        }

        [Test]
        public void Compare_NamesMissingExtraAndField()
        {
            var store = new EntityStore();
            store.Reset(new PlayerState(Player, 400, new Point3D(5445, 1153, 0), Direction.North, new MapSize(7168, 4096)));
            store.UpdateMobile(Player, new MobileInfo(400, new Point3D(5445, 1153, 0), Direction.North, 33771, 0, 1));
            store.PlaceInWorld(new Serial(0x40000099), new ItemInfo(0x09D0, 1, new Point3D(5445, 1153, 0), 0, 0, 0));

            var differences = WorldSnapshot.Compare(WorldSnapshot.FromStore(store), WorldSnapshot.Read(ServerJson));

            Assert.That(differences, Has.Some.Contains("0x00000001").And.Contains("hue").And.Contains("33771").And.Contains("33770"));
            Assert.That(differences, Has.Some.Contains("0x40000001").And.Contains("нет у бота"));
            Assert.That(differences, Has.Some.Contains("0x40000099").And.Contains("нет на сервере"));
        }

        [Test]
        public void Stub_ReportedAsUnplaced()
        {
            var store = new EntityStore();
            store.PlaceInContainer(new Serial(0x40000002), new ItemInfo(4081, 1, Point3D.Zero, 0, 0, 0), new Serial(0x40000050), 0);

            var stub = WorldSnapshot.FromStore(store).Objects.Single(o => o.Serial.Value == 0x40000050u);

            Assert.That(stub.Place, Is.EqualTo("unplaced"));
        }

        [Test]
        public void WriteThenRead_SameObjects()
        {
            var store = new EntityStore();
            store.Reset(new PlayerState(Player, 400, new Point3D(1, 2, 3), Direction.North, new MapSize(7168, 4096)));
            store.PlaceInWorld(new Serial(0x40000099), new ItemInfo(0x09D0, 7, new Point3D(1, 2, -3), 0x21, 0, 0));
            var snapshot = WorldSnapshot.FromStore(store);

            var back = WorldSnapshot.Read(WorldSnapshot.Write(snapshot));

            Assert.That(WorldSnapshot.Compare(back, snapshot), Is.Empty);
            Assert.That(back.Player, Is.EqualTo(Player));
        }
    }
}
