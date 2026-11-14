using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.MapView;
using TheVerge.Core.Multis;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using static TheVerge.Tests.Core.World.WorldSamples;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>
    /// Окно клетки отладочной карты (PLAN Стадия 2, п. 13): все объекты на клетке — земля, статика, части домов, предметы
    /// на земле, мобы — с номером плитки, Z, высотой, флагами и именем из tiledata. Данные те же, что у сервера.
    /// </summary>
    public sealed class CellInspectorTests
    {
        private FakeMap _map = null!;
        private FakeTiles _tiles = null!;
        private FakeMultis _multis = null!;
        private EntityStore _world = null!;
        private CellInspector _inspector = null!;
        private readonly List<CellObject> _objects = new List<CellObject>();

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(200, 200, new LandTile(3, 5));
            _tiles = new FakeTiles();
            _tiles.SetLand(3, TileFlags.None, "grass");
            _multis = new FakeMultis();
            _world = new EntityStore();
            _world.ChangeMap(0);
            _inspector = new CellInspector(_map, _tiles, _multis, _world);
        }

        [Test]
        public void EmptyCell_HasOnlyLand()
        {
            var onMap = _inspector.TryCollect(100, 100, _objects);

            Assert.That(onMap, Is.True);
            Assert.That(_objects, Has.Count.EqualTo(1));
            Assert.That((_objects[0].Kind, _objects[0].Id, _objects[0].Z, _objects[0].Name),
                Is.EqualTo((SceneKind.Land, 3, 5, "grass")));
            Assert.That(_objects[0].Height, Is.Null);
        }

        [Test]
        public void CellOutsideMap_IsReported()
        {
            Assert.That(_inspector.TryCollect(500, 500, _objects), Is.False);
            Assert.That(_inspector.Describe(500, 500), Is.EqualTo("Клетка (500, 500): за пределами карты (200 × 200), данных нет."));
        }

        [Test]
        public void Statics_KeepFileOrder_WithHeightFlagsHueAndName()
        {
            _tiles.SetItem(0x0007, TileFlags.Wall | TileFlags.Impassable, 20, "wall");
            _tiles.SetItem(0x000A, TileFlags.Surface, 3, "slab");
            _map.AddStatic(100, 100, new StaticTile(0x0007, 10, 33));
            _map.AddStatic(100, 100, new StaticTile(0x000A, 2, 0));

            _inspector.TryCollect(100, 100, _objects);

            Assert.That(_objects, Has.Count.EqualTo(3));
            var first = _objects[1];
            Assert.That((first.Kind, first.Id, first.Z, first.Height, first.Flags, first.Hue, first.Name),
                Is.EqualTo((SceneKind.Static, 7, 10, (int?)20, TileFlags.Wall | TileFlags.Impassable, 33, "wall")));
            Assert.That((_objects[2].Id, _objects[2].Z, _objects[2].Height), Is.EqualTo((0x0A, 2, (int?)3)));
        }

        [Test]
        public void BridgeStatic_ShowsHalfHeight()
        {
            _tiles.SetItem(0x0008, TileFlags.Bridge, 10, "bridge");
            _map.AddStatic(100, 100, new StaticTile(0x0008, 0, 0));

            _inspector.TryCollect(100, 100, _objects);

            Assert.That(_objects[1].Height, Is.EqualTo(5));
        }

        [Test]
        public void HousePart_IsListedOnItsShiftedCellOnly()
        {
            _tiles.SetItem(0x0001, TileFlags.Wall | TileFlags.Impassable, 20, "wall");
            _multis.Set(0x10, new MultiComponent(0x0001, 0, 0, 0, TileFlags.Background), new MultiComponent(0x0001, 2, -1, 10, TileFlags.Background));
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new Point3D(100, 100, 5), 0, 0, 0, isMulti: true));

            _inspector.TryCollect(102, 99, _objects);
            var part = _objects.Find(o => o.Kind == SceneKind.MultiPart);

            Assert.That((part.Id, part.Z, part.Height, part.Serial), Is.EqualTo((1, 15, (int?)20, Backpack.Value)));
            _inspector.TryCollect(101, 99, _objects);
            Assert.That(_objects.Exists(o => o.Kind == SceneKind.MultiPart), Is.False);
        }

        [Test]
        public void GroundItem_ShowsSerialAmountAndTileName()
        {
            _tiles.SetItem(0x09D0, TileFlags.None, 0, "apple");
            _world.PlaceInWorld(Apple, new ItemInfo(0x09D0, 3, new Point3D(100, 100, 5), 0x0021, 0, 0));

            _inspector.TryCollect(100, 100, _objects);
            var item = _objects.Find(o => o.Kind == SceneKind.Item);

            Assert.That((item.Serial, item.Id, item.Z, item.Amount, item.Hue, item.Name),
                Is.EqualTo((Apple.Value, 0x09D0, 5, 3, 0x0021, "apple")));
        }

        [Test]
        public void ItemInContainerAndHouseItself_AreNotListedAsItems()
        {
            _world.PlaceInWorld(Box, Thing(0x0E75, 100, 100, 0));
            _world.PlaceInContainer(Apple, Thing(0x09D0), Box, 0);
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new Point3D(100, 100, 0), 0, 0, 0, isMulti: true));

            _inspector.TryCollect(100, 100, _objects);

            Assert.That(_objects.FindAll(o => o.Kind == SceneKind.Item), Has.Count.EqualTo(1));
        }

        [Test]
        public void Mobile_ShowsBodySerialAndHue_AndOnlyOnItsCell()
        {
            _world.UpdateMobile(Orc, Mob(100, 100, 3));

            _inspector.TryCollect(100, 100, _objects);
            var mobile = _objects.Find(o => o.Kind == SceneKind.Mobile);

            Assert.That((mobile.Serial, mobile.Id, mobile.Z, mobile.Hue), Is.EqualTo((Orc.Value, 0x0190, 3, 0x83EA)));
            _inspector.TryCollect(101, 100, _objects);
            Assert.That(_objects.Exists(o => o.Kind == SceneKind.Mobile), Is.False);
        }

        [Test]
        public void Order_LandStaticPartItemMobile()
        {
            _map.AddStatic(100, 100, new StaticTile(0x0007, 0, 0));
            _multis.Set(0x10, new MultiComponent(0x0001, 0, 0, 0, TileFlags.Background));
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new Point3D(100, 100, 0), 0, 0, 0, isMulti: true));
            _world.PlaceInWorld(Apple, Thing(0x09D0, 100, 100, 0));
            _world.UpdateMobile(Orc, Mob(100, 100));

            _inspector.TryCollect(100, 100, _objects);

            Assert.That(_objects.ConvertAll(o => o.Kind), Is.EqualTo(new[]
                { SceneKind.Land, SceneKind.Static, SceneKind.MultiPart, SceneKind.Item, SceneKind.Mobile }));
        }

        [Test]
        public void Describe_LandAndStatic_ExactText()
        {
            _tiles.SetItem(0x0007, TileFlags.Wall | TileFlags.Impassable, 20, "wall");
            _map.AddStatic(100, 100, new StaticTile(0x0007, 10, 0));

            var text = _inspector.Describe(100, 100);

            Assert.That(text, Is.EqualTo(
                "Клетка (100, 100), карта 0 — объектов: 2\n" +
                "земля: tile 0x0003 «grass», Z 5, флаги: нет\n" +
                "статика: tile 0x0007 «wall», Z 10, высота 20 (верх 30), цвет 0x0000, флаги: Wall, Impassable"));
        }

        [Test]
        public void Describe_HousePartItemAndMobile_ExactText()
        {
            _tiles.SetItem(0x0001, TileFlags.Wall | TileFlags.Impassable, 20, "wall");
            _tiles.SetItem(0x09D0, TileFlags.None, 0, "apple");
            _multis.Set(0x10, new MultiComponent(0x0001, 0, 0, 10, TileFlags.Background));
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new Point3D(100, 100, 5), 0, 0, 0, isMulti: true));
            _world.PlaceInWorld(Apple, Thing(0x09D0, 100, 100, 5));
            _world.UpdateMobile(Orc, Mob(100, 100));

            var text = _inspector.Describe(100, 100);

            Assert.That(text, Does.Contain("\nчасть дома 0x40000010: tile 0x0001 «wall», Z 15, высота 20 (верх 35), флаги: Wall, Impassable\n"));
            Assert.That(text, Does.Contain("\nпредмет 0x40000022: tile 0x09D0 «apple», Z 5, высота 0 (верх 5), кол-во 1, цвет 0x0000, флаги: нет\n"));
            Assert.That(text, Does.EndWith("\nмоб 0x00000100: тело 0x0190, Z 0, цвет 0x83EA"));
        }

        [Test]
        public void Describe_UnknownTileName_OmitsQuotes()
        {
            _map.AddStatic(100, 100, new StaticTile(0x0123, 0, 0));

            Assert.That(_inspector.Describe(100, 100), Does.Contain("\nстатика: tile 0x0123, Z 0, высота 0 (верх 0), цвет 0x0000, флаги: нет"));
        }

        [Test]
        public void Describe_OtherMapThanFelucca_SaysThereIsNoData()
        {
            _world.ChangeMap(1);

            Assert.That(_inspector.Describe(100, 100), Is.EqualTo("Клетка (100, 100): карта 1 — данных нет (есть только Фелукка)."));
        }

        [Test]
        public void Describe_MapNotYetKnown_OmitsMapNumber()
        {
            _world.Reset(PlayerAt(10, 10));

            Assert.That(_inspector.Describe(100, 100), Does.StartWith("Клетка (100, 100) — объектов: 1\n"));
        }
    }
}
