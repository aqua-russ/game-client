using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Movement.Tooling;
using TheVerge.Core.Multis;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Поиск пути A* (<see cref="PathFinder"/>): подъём и спуск по высоте Z, перекрывающиеся уровни (под мостом и по мосту),
    /// двери, сундуки, дома (multi), живые мобы и отсутствие выделений памяти в установившемся режиме.
    /// </summary>
    public sealed class PathFinderZAndEntitiesTests
    {
        private const int Grass = 3;
        private const int Floor = 0x0001;
        private const int Wall = 0x0002;
        private const int TallStair = 0x0016;
        private const int MidStair = 0x0017;
        private const int Door = 0x0675;
        private const int Chest = 0x0E40;

        private FakeMap _map = null!;
        private FakeTiles _tiles = null!;
        private FakeMultis _multis = null!;
        private EntityStore _entities = null!;
        private MovementCheck _check = null!;
        private PathFinder _finder = null!;
        private List<PathMove> _path = null!;

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(128, 128, new LandTile(Grass, 0));
            _tiles = new FakeTiles();
            _multis = new FakeMultis();
            _entities = new EntityStore();
            _entities.ChangeMap(0);

            _tiles.SetLand(Grass, TileFlags.None, "grass");
            _tiles.SetItem(Floor, TileFlags.Surface, 0, "floor");
            _tiles.SetItem(Wall, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            _tiles.SetItem(TallStair, TileFlags.Surface | TileFlags.Bridge, 16, "tallStair");
            _tiles.SetItem(MidStair, TileFlags.Surface | TileFlags.Bridge, 8, "midStair");
            _tiles.SetItem(Door, TileFlags.Door | TileFlags.Impassable | TileFlags.Wall, 20, "door");
            _tiles.SetItem(Chest, TileFlags.Impassable | TileFlags.Surface, 4, "chest");

            _check = new MovementCheck(_map, _tiles, _multis, _entities);
            _finder = new PathFinder(_check);
            _path = new List<PathMove>(64);
        }

        [Test]
        public void StairsAndElevation_ClimbsToTargetZAndDescendsBack()
        {
            for (int y = 8; y <= 14; y++)
            {
                if (y != 12)
                {
                    _map.AddStatic(12, y, new StaticTile(Wall, 0, 0));
                }
            }

            _map.AddStatic(12, 12, new StaticTile(TallStair, 0, 0));  // Z = 8
            _map.AddStatic(13, 12, new StaticTile(TallStair, 8, 0));  // Z = 16
            for (int y = 9; y <= 12; y++)
            {
                _map.AddStatic(14, y, new StaticTile(Floor, 16, 0));
            }

            var start = new Point3D(10, 10, 0);
            var up = _finder.FindPath(start, new PathGoal(14, 10, 16), MoverState.Player(), _path);

            Assert.That(up.IsSuccess, Is.True);
            Assert.That(up.Reached, Is.EqualTo(new Point3D(14, 10, 16)));
            AssertAllStepsValid(start, MoverState.Player());

            var down = _finder.FindPath(up.Reached, new PathGoal(10, 10, 0), MoverState.Player(), _path);
            Assert.That(down.IsSuccess, Is.True);
            Assert.That(down.Reached, Is.EqualTo(start));
            AssertAllStepsValid(up.Reached, MoverState.Player());
        }

        [Test]
        public void OverlappingLevels_SameXYDifferentZ_RoutesUnderThenOverBridge()
        {
            for (int y = 8; y <= 13; y++)
            {
                if (y != 10)
                {
                    _map.AddStatic(12, y, new StaticTile(Wall, 0, 0));
                }
            }

            _map.AddStatic(11, 9, new StaticTile(Wall, 0, 0));
            _map.AddStatic(11, 11, new StaticTile(Wall, 0, 0));

            _map.AddStatic(13, 10, new StaticTile(TallStair, 0, 0));   // Z = 8
            _map.AddStatic(14, 10, new StaticTile(TallStair, 8, 0));   // Z = 16
            _map.AddStatic(14, 11, new StaticTile(MidStair, 16, 0));   // Z = 20
            _map.AddStatic(13, 11, new StaticTile(Floor, 20, 0));
            _map.AddStatic(12, 11, new StaticTile(Floor, 20, 0));
            _map.AddStatic(12, 10, new StaticTile(Floor, 20, 0));

            var start = new Point3D(10, 10, 0);
            var result = _finder.FindPath(start, new PathGoal(12, 10, 20), MoverState.Player(), _path);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Reached, Is.EqualTo(new Point3D(12, 10, 20)));
            Assert.That(_path.Exists(m => m.To == new Point3D(12, 10, 0)), Is.True, "сначала проходит под мостом");
            Assert.That(_path[_path.Count - 1].To, Is.EqualTo(new Point3D(12, 10, 20)));
            AssertAllStepsValid(start, MoverState.Player());
        }

        [Test]
        public void DoorsMultisAndMobs_PlayerRoutesAround_GhostPassesDoorAndMob()
        {
            var start = new Point3D(10, 10, 0);
            _map.AddStatic(12, 12, new StaticTile(Wall, 0, 0));
            _entities.PlaceInWorld(new Serial(0x40000100), new ItemInfo(Door, 1, new Point3D(12, 10, 0), 0, 0, 0));
            _entities.PlaceInWorld(new Serial(0x40000101), new ItemInfo(Chest, 1, new Point3D(12, 13, 0), 0, 0x20, 0));
            _entities.UpdateMobile(new Serial(0x00000020), new MobileInfo(0x00EE, new Point3D(12, 11, 0), Direction.West, 0, 0, 1));
            _multis.Set(0x0010, new MultiComponent(Wall, 0, 0, 0, TileFlags.Impassable | TileFlags.Wall));
            _entities.PlaceInWorld(new Serial(0x40000102), new ItemInfo(0x0010, 1, new Point3D(12, 8, 0), 0, 0, 0, isMulti: true));

            var player = MoverState.Player(serial: 1, canShoveMobiles: false);
            var playerResult = _finder.FindPath(start, new PathGoal(14, 10), player, _path);

            Assert.That(playerResult.IsSuccess, Is.True);
            Assert.That(_path.Exists(m => m.To.X == 12 && m.To.Y == 10), Is.False, "закрытая дверь");
            Assert.That(_path.Exists(m => m.To.X == 12 && m.To.Y == 11), Is.False, "живой моб");
            Assert.That(_path.Exists(m => m.To.X == 12 && m.To.Y == 8), Is.False, "стена multi");
            Assert.That(_path.Exists(m => m.To.X == 12 && m.To.Y == 9), Is.True, "идёт через открытый проход");
            AssertAllStepsValid(start, player);

            var ghost = MoverState.Ghost(serial: 1);
            var ghostResult = _finder.FindPath(start, new PathGoal(14, 10), ghost, _path);
            Assert.That(ghostResult.IsSuccess, Is.True);
            Assert.That(ghostResult.Steps, Is.EqualTo(4), "призрак идёт прямо сквозь закрытую дверь");
            AssertAllStepsValid(start, ghost);
        }

        [Test]
        public void SteadyState_RepeatedSearches_ZeroAllocations()
        {
            for (int y = 5; y <= 25; y++)
            {
                if (y != 25)
                {
                    _map.AddStatic(18, y, new StaticTile(Wall, 0, 0));
                }
            }

            var start = new Point3D(10, 10, 0);
            var goal = new PathGoal(26, 10);
            _path.Capacity = 128;

            for (int i = 0; i < 4; i++)
            {
                Assert.That(_finder.FindPath(start, goal, MoverState.Player(), _path).IsSuccess, Is.True);
            }

            bool allOk = true;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 30; i++)
            {
                allOk &= _finder.FindPath(start, goal, MoverState.Player(), _path).IsSuccess;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allOk, Is.True);
            Assert.That(allocated, Is.EqualTo(0));
        }

        private void AssertAllStepsValid(Point3D start, MoverState mover)
        {
            Assert.That(_path, Is.Not.Empty);
            var cursor = start;
            for (int i = 0; i < _path.Count; i++)
            {
                var move = _path[i];
                Assert.That(move.From, Is.EqualTo(cursor));
                bool ok = _check.CheckMovement(cursor, move.Direction, mover, out int nextZ);
                Assert.That(ok, Is.True);
                var step = cursor.GetStepPoint(move.Direction);
                var expected = new Point3D(step.X, step.Y, (sbyte)nextZ);
                Assert.That(move.To, Is.EqualTo(expected));
                cursor = expected;
            }
        }
    }
}
