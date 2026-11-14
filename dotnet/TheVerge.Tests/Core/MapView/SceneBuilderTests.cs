using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.MapView;
using TheVerge.Core.Multis;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using static TheVerge.Tests.Core.World.WorldSamples;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>
    /// Блоки отладочной карты (PLAN Стадия 2, п. 13; ADR 0015 п. 3): верх блока там, где его видит сервер — Z земли и Z
    /// статики плюс высота плитки (ItemData.CalcHeight: у моста половина; Projects/UOContent/Misc/TileData.cs).
    /// </summary>
    public sealed class SceneBuilderTests
    {
        private FakeMap _map = null!;
        private FakeTiles _tiles = null!;
        private FakeMultis _multis = null!;
        private EntityStore _world = null!;
        private SceneBuilder _builder = null!;
        private readonly List<SceneBlock> _blocks = new List<SceneBlock>();

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(200, 200, new LandTile(3, 5));
            _tiles = new FakeTiles();
            _multis = new FakeMultis();
            _world = new EntityStore();
            _builder = new SceneBuilder(_map, _tiles, _multis, _world);
        }

        [Test]
        public void FlatLand_OneBlockPerCell_TopAtLandZ()
        {
            _builder.Build(100, 100, 2, _blocks);

            Assert.That(_blocks, Has.Count.EqualTo(25));
            Assert.That(_blocks.All(b => b.Kind == SceneKind.Land && b.ZTop == 5 && b.ZBottom == 3), Is.True);
            Assert.That(_blocks.All(b => b.Class == CellClass.Surface), Is.True);
            Assert.That(_blocks.Select(b => (b.X, b.Y)).Distinct().Count(), Is.EqualTo(25));
        }

        [Test]
        public void ViewRadius18_Is37By37Cells()
        {
            _builder.Build(100, 100, 18, _blocks);

            Assert.That(_blocks, Has.Count.EqualTo(37 * 37));
        }

        [Test]
        public void Build_ClearsPreviousResult()
        {
            _blocks.Add(new SceneBlock(SceneKind.Item, 1, 1, 0, 1, CellClass.Other));

            _builder.Build(100, 100, 1, _blocks);

            Assert.That(_blocks, Has.Count.EqualTo(9));
        }

        [Test]
        public void CellsOutsideMap_AreSkipped()
        {
            _builder.Build(0, 0, 2, _blocks);

            Assert.That(_blocks, Has.Count.EqualTo(9));
            Assert.That(_blocks.All(b => b.X >= 0 && b.Y >= 0), Is.True);
        }

        [Test]
        public void HigherCell_ReachesDownToLowestNeighbour()
        {
            _map.SetLand(100, 100, new LandTile(3, 10));
            _map.SetLand(101, 100, new LandTile(3, 0));

            _builder.Build(100, 100, 1, _blocks);

            var high = _blocks.Single(b => b.X == 100 && b.Y == 100);
            Assert.That((high.ZBottom, high.ZTop), Is.EqualTo((0, 10)));
        }

        [Test]
        public void LowestCell_HasMinimumThickness()
        {
            _map.SetLand(100, 100, new LandTile(3, 0));
            _map.SetLand(101, 100, new LandTile(3, 10));

            _builder.Build(100, 100, 1, _blocks);

            var low = _blocks.Single(b => b.X == 100 && b.Y == 100);
            Assert.That((low.ZBottom, low.ZTop), Is.EqualTo((-2, 0)));
        }

        [Test]
        public void MapEdge_NeighbourOutsideMapIsIgnored()
        {
            _builder.Build(0, 0, 0, _blocks);

            Assert.That((_blocks.Single().ZBottom, _blocks.Single().ZTop), Is.EqualTo((3, 5)));
        }

        [Test]
        public void WetLand_IsWater()
        {
            _tiles.SetLand(0xA8, TileFlags.Wet | TileFlags.Impassable);
            _map.SetLand(100, 100, new LandTile(0xA8, -5));

            _builder.Build(100, 100, 0, _blocks);

            Assert.That(_blocks.Single().Class, Is.EqualTo(CellClass.Water));
        }

        [Test]
        public void StaticWall_RisesByTileHeight()
        {
            _tiles.SetItem(0x0007, TileFlags.Wall | TileFlags.Impassable, 20);
            _map.AddStatic(100, 100, new StaticTile(0x0007, 10, 0));

            _builder.Build(100, 100, 0, _blocks);

            var wall = _blocks.Single(b => b.Kind == SceneKind.Static);
            Assert.That((wall.ZBottom, wall.ZTop, wall.Class), Is.EqualTo((10, 30, CellClass.Wall)));
        }

        [Test]
        public void BridgeStatic_UsesHalfHeight()
        {
            _tiles.SetItem(0x0008, TileFlags.Bridge | TileFlags.Surface, 10);
            _map.AddStatic(100, 100, new StaticTile(0x0008, 0, 0));

            _builder.Build(100, 100, 0, _blocks);

            Assert.That(_blocks.Single(b => b.Kind == SceneKind.Static).ZTop, Is.EqualTo(5));
        }

        [Test]
        public void ZeroHeightStatic_IsDrawnAsThinPlate()
        {
            _map.AddStatic(100, 100, new StaticTile(0x0009, 7, 0));

            _builder.Build(100, 100, 0, _blocks);

            var plate = _blocks.Single(b => b.Kind == SceneKind.Static);
            Assert.That((plate.ZBottom, plate.ZTop), Is.EqualTo((7, 8)));
        }

        [Test]
        public void Statics_KeepFileOrderAfterLand()
        {
            _tiles.SetItem(0x000A, TileFlags.None, 2);
            _tiles.SetItem(0x000B, TileFlags.None, 3);
            _map.AddStatic(100, 100, new StaticTile(0x000B, 20, 0));
            _map.AddStatic(100, 100, new StaticTile(0x000A, 5, 0));

            _builder.Build(100, 100, 0, _blocks);

            Assert.That(_blocks.Select(b => (b.Kind, b.ZBottom)),
                Is.EqualTo(new[] { (SceneKind.Land, 3), (SceneKind.Static, 20), (SceneKind.Static, 5) }));
        }

        [Test]
        public void StaticOutsideView_IsNotBuilt()
        {
            _map.AddStatic(110, 110, new StaticTile(0x000A, 5, 0));

            _builder.Build(100, 100, 2, _blocks);

            Assert.That(_blocks.Any(b => b.Kind == SceneKind.Static), Is.False);
        }

        [Test]
        public void Multi_PartsLandOnShiftedCells()
        {
            _tiles.SetItem(0x0001, TileFlags.Wall | TileFlags.Impassable, 20);
            _multis.Set(0x10, new MultiComponent(0x0001, 0, 0, 0, TileFlags.Background), new MultiComponent(0x0001, 2, -1, 10, TileFlags.Background));
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new TheVerge.Core.Primitives.Point3D(100, 100, 5), 0, 0, 0, isMulti: true));

            _builder.Build(100, 100, 3, _blocks);

            var parts = _blocks.Where(b => b.Kind == SceneKind.MultiPart).OrderBy(b => b.X).ToList();
            Assert.That(parts.Select(p => (p.X, p.Y, p.ZBottom, p.ZTop, p.Class)),
                Is.EqualTo(new[] { (100, 100, 5, 25, CellClass.Wall), (102, 99, 15, 35, CellClass.Wall) }));
        }

        [Test]
        public void Multi_PartInsideViewWhileHouseCentreIsOutside()
        {
            _multis.Set(0x10, new MultiComponent(0x0001, 3, 0, 0, TileFlags.Background));
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new TheVerge.Core.Primitives.Point3D(100, 100, 0), 0, 0, 0, isMulti: true));

            _builder.Build(104, 100, 2, _blocks);

            Assert.That(_blocks.Count(b => b.Kind == SceneKind.MultiPart && b.X == 103 && b.Y == 100), Is.EqualTo(1));
        }

        [Test]
        public void Multi_PartOutsideView_IsSkipped()
        {
            _multis.Set(0x10, new MultiComponent(0x0001, 15, 0, 0, TileFlags.Background));
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new TheVerge.Core.Primitives.Point3D(100, 100, 0), 0, 0, 0, isMulti: true));

            _builder.Build(100, 100, 3, _blocks);

            Assert.That(_blocks.Any(b => b.Kind == SceneKind.MultiPart), Is.False);
        }

        [Test]
        public void OrdinaryGroundItem_IsNotABlock()
        {
            _world.PlaceInWorld(Apple, Thing(0x09D0, 100, 100, 5));

            _builder.Build(100, 100, 1, _blocks);

            Assert.That(_blocks.All(b => b.Kind == SceneKind.Land), Is.True);
        }
    }
}
