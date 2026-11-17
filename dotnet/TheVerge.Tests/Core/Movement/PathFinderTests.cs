using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Movement.Tooling;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Служебный поиск пути A* (<see cref="PathFinder"/>) поверх физики <see cref="MovementCheck"/>:
    /// диагонали и прямые, обход стен и углов, цель вне карты, закрытая комната и предел поиска.
    /// </summary>
    public sealed class PathFinderTests
    {
        private const int Grass = 3;
        private const int Wall = 0x0002;

        private FakeMap _map = null!;
        private FakeTiles _tiles = null!;
        private MovementCheck _check = null!;
        private PathFinder _finder = null!;
        private List<PathMove> _path = null!;

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(128, 128, new LandTile(Grass, 0));
            _tiles = new FakeTiles();
            _tiles.SetLand(Grass, TileFlags.None, "grass");
            _tiles.SetItem(Wall, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            _check = new MovementCheck(_map, _tiles);
            _finder = new PathFinder(_check);
            _path = new List<PathMove>(64);
        }

        [Test]
        public void OpenGround_UsesDiagonalsAndMinimalTurns()
        {
            var start = new Point3D(10, 10, 0);

            var straight = _finder.FindPath(start, new PathGoal(15, 10), MoverState.Player(), _path);
            Assert.That(straight.Status, Is.EqualTo(PathStatus.Found));
            Assert.That(straight.Steps, Is.EqualTo(5));
            Assert.That(straight.Reached, Is.EqualTo(new Point3D(15, 10, 0)));
            Assert.That(_path.TrueForAll(m => m.Direction == Direction.East), Is.True);

            var diagonal = _finder.FindPath(start, new PathGoal(15, 15), MoverState.Player(), _path);
            Assert.That(diagonal.Status, Is.EqualTo(PathStatus.Found));
            Assert.That(diagonal.Steps, Is.EqualTo(5));
            Assert.That(_path.TrueForAll(m => m.Direction == Direction.Down), Is.True);

            var mixed = _finder.FindPath(start, new PathGoal(14, 12), MoverState.Player(), _path);
            Assert.That(mixed.Status, Is.EqualTo(PathStatus.Found));
            Assert.That(mixed.Steps, Is.EqualTo(4), "чебышёвский минимум 4 шага вместо 6 по прямой");
            AssertAllStepsValid(start, MoverState.Player());
        }

        [Test]
        public void AlreadyAtGoal_ReturnsFoundWithZeroSteps()
        {
            var start = new Point3D(10, 10, 0);

            var result = _finder.FindPath(start, new PathGoal(10, 10, 0), MoverState.Player(), _path);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Steps, Is.EqualTo(0));
            Assert.That(result.Reached, Is.EqualTo(start));
            Assert.That(_path, Is.Empty);
        }

        [Test]
        public void WallAndUShapePocket_DetoursAroundObstacle()
        {
            for (int y = 8; y <= 13; y++)
            {
                _map.AddStatic(13, y, new StaticTile(Wall, 0, 0));
            }

            for (int x = 10; x <= 13; x++)
            {
                _map.AddStatic(x, 8, new StaticTile(Wall, 0, 0));
                _map.AddStatic(x, 13, new StaticTile(Wall, 0, 0));
            }

            var start = new Point3D(12, 10, 0);
            var result = _finder.FindPath(start, new PathGoal(16, 10), MoverState.Player(), _path);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Reached, Is.EqualTo(new Point3D(16, 10, 0)));
            Assert.That(_path.Count, Is.EqualTo(result.Steps));
            AssertAllStepsValid(start, MoverState.Player());
        }

        [Test]
        public void DiagonalCorner_PlayerRoutesAroundCorner_GmCutsThrough()
        {
            var start = new Point3D(10, 10, 0);
            _map.AddStatic(10, 9, new StaticTile(Wall, 0, 0));

            var player = _finder.FindPath(start, new PathGoal(11, 9), MoverState.Player(), _path);
            Assert.That(player.IsSuccess, Is.True);
            Assert.That(player.Steps, Is.EqualTo(2), "игроку нельзя срезать угол по диагонали");
            AssertAllStepsValid(start, MoverState.Player());

            var gm = _finder.FindPath(start, new PathGoal(11, 9), MoverState.GameMaster(), _path);
            Assert.That(gm.IsSuccess, Is.True);
            Assert.That(gm.Steps, Is.EqualTo(1), "GM срезает угол при одном свободном соседе");
            Assert.That(_path[0].Direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        public void OutOfBoundsAndUnreachable_FailQuicklyWithReason()
        {
            var start = new Point3D(10, 10, 0);

            var oob = _finder.FindPath(start, new PathGoal(-1, 10), MoverState.Player(), _path);
            Assert.That(oob.Status, Is.EqualTo(PathStatus.OutOfBounds));
            Assert.That(_path, Is.Empty);

            var oobMax = _finder.FindPath(start, new PathGoal(128, 10), MoverState.Player(), _path);
            Assert.That(oobMax.Status, Is.EqualTo(PathStatus.OutOfBounds));

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        _map.AddStatic(20 + dx, 20 + dy, new StaticTile(Wall, 0, 0));
                    }
                }
            }

            var walled = _finder.FindPath(new Point3D(20, 20, 0), new PathGoal(25, 20), MoverState.Player(), _path);
            Assert.That(walled.Status, Is.EqualTo(PathStatus.Unreachable));
            Assert.That(_path, Is.Empty);
        }

        [Test]
        public void BeyondSearchBudgetOrMaxDistance_ReturnsLimitReached()
        {
            var limited = new PathFinder(_check, maxExpandedNodes: 24, maxDistance: 40);

            var tooFar = limited.FindPath(new Point3D(10, 10, 0), new PathGoal(60, 10), MoverState.Player(), _path);
            Assert.That(tooFar.Status, Is.EqualTo(PathStatus.LimitReached));
            Assert.That(_path, Is.Empty);

            var budget = limited.FindPath(new Point3D(10, 10, 0), new PathGoal(45, 10), MoverState.Player(), _path);
            Assert.That(budget.Status, Is.EqualTo(PathStatus.LimitReached));
            Assert.That(budget.ExpandedNodes, Is.LessThanOrEqualTo(24));
        }

        private void AssertAllStepsValid(Point3D start, MoverState mover)
        {
            Assert.That(_path, Is.Not.Empty);
            var cursor = start;
            for (int i = 0; i < _path.Count; i++)
            {
                var move = _path[i];
                Assert.That(move.From, Is.EqualTo(cursor), "непрерывность шага " + i);
                bool ok = _check.CheckMovement(cursor, move.Direction, mover, out int nextZ);
                Assert.That(ok, Is.True, "шаг " + i + " должен проходить MovementCheck");
                var step = cursor.GetStepPoint(move.Direction);
                var expected = new Point3D(step.X, step.Y, (sbyte)nextZ);
                Assert.That(move.To, Is.EqualTo(expected));
                cursor = expected;
            }
        }
    }
}
