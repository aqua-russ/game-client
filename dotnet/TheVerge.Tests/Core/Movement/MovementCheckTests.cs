using System;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Физика движения <see cref="MovementCheck"/> против правил ModernUO (Engines/Pathing/Movement.cs):
    /// границы карты, земля, наклоны, статика, мосты/лестницы, высота потолка 16, диагональные углы.
    /// </summary>
    public sealed class MovementCheckTests
    {
        private FakeMap _map = null!;
        private FakeTiles _tiles = null!;
        private MovementCheck _check = null!;

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(64, 64, new LandTile(3, 0));
            _tiles = new FakeTiles();
            _tiles.SetLand(3, TileFlags.None, "grass");
            _tiles.SetLand(4, TileFlags.Impassable, "rock");
            _tiles.SetLand(5, TileFlags.Impassable | TileFlags.Wet, "water");
            _tiles.SetLand(6, TileFlags.Wet, "acid");
            _tiles.SetItem(0x0010, TileFlags.Surface, 2, "step2");
            _tiles.SetItem(0x0011, TileFlags.Surface, 3, "step3");
            _tiles.SetItem(0x0012, TileFlags.Surface | TileFlags.Bridge, 4, "stair");
            _tiles.SetItem(0x0013, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            _tiles.SetItem(0x0014, TileFlags.Surface | TileFlags.Roof, 1, "ceiling");
            _tiles.SetItem(0x0015, TileFlags.Surface | TileFlags.Impassable, 2, "table");
            _check = new MovementCheck(_map, _tiles);
        }

        [Test]
        public void FlatLand_CardinalAndDiagonal_Succeeds()
        {
            var start = new Point3D(10, 10, 0);

            Assert.That(_check.CheckMovement(start, Direction.North, out int northZ), Is.True);
            Assert.That(northZ, Is.EqualTo(0));
            Assert.That(_check.CheckMovement(start, Direction.Right, out int diagZ), Is.True);
            Assert.That(diagZ, Is.EqualTo(0));
        }

        [Test]
        public void OutOfMapBounds_ReturnsFalseAndZeroZ()
        {
            Assert.That(_check.CheckMovement(new Point3D(0, 0, 5), Direction.North, out int topZ), Is.False);
            Assert.That(topZ, Is.EqualTo(0));

            Assert.That(_check.CheckMovement(new Point3D(63, 63, 5), Direction.South, out int bottomZ), Is.False);
            Assert.That(bottomZ, Is.EqualTo(0));
        }

        [Test]
        public void ImpassableLand_BlocksAndPreservesStartZ()
        {
            _map.SetLand(11, 10, new LandTile(4, 0));

            bool ok = _check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out int newZ);

            Assert.That(ok, Is.False);
            Assert.That(newZ, Is.EqualTo(0));
        }

        [Test]
        public void WaterLand_BlocksWalkingPlayer_WetWithoutImpassable_IsWalkable()
        {
            _map.SetLand(11, 10, new LandTile(5, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out _), Is.False);

            _map.SetLand(11, 12, new LandTile(6, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 12, 0), Direction.East, out _), Is.True);
        }

        [Test]
        public void IgnoredLandId_RequiresStaticSurface()
        {
            _map.SetLand(11, 10, new LandTile(2, 0));

            Assert.That(_check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out _), Is.False);

            _map.AddStatic(11, 10, new StaticTile(0x0010, 0, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out int newZ), Is.True);
            Assert.That(newZ, Is.EqualTo(2));
        }

        [Test]
        public void SlopedLand_ClimbWithinStepHeight_Succeeds_SteepSlope_Blocks()
        {
            SetLandBlock(11, 10, 2);
            Assert.That(_check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out int upZ), Is.True);
            Assert.That(upZ, Is.EqualTo(2));

            SetLandBlock(11, 12, 3);
            Assert.That(_check.CheckMovement(new Point3D(10, 12, 0), Direction.East, out _), Is.False);

            SetLandBlock(10, 20, 20);
            SetLandBlock(11, 20, 0);
            Assert.That(_check.CheckMovement(new Point3D(10, 20, 20), Direction.East, out int downZ), Is.True);
            Assert.That(downZ, Is.EqualTo(0));
        }

        [Test]
        public void StepUpStaticSurface_UpTo2_Succeeds_StepUp3_Blocks()
        {
            _map.AddStatic(11, 10, new StaticTile(0x0010, 0, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out int step2Z), Is.True);
            Assert.That(step2Z, Is.EqualTo(2));

            _map.AddStatic(11, 12, new StaticTile(0x0011, 0, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 12, 0), Direction.East, out _), Is.False);
        }

        [Test]
        public void BridgeOrStairs_Surface_WalkableAtHalfHeight_ImpassableSurfaceBlocks()
        {
            _map.AddStatic(11, 10, new StaticTile(0x0012, 0, 0));

            bool ok = _check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out int newZ);

            Assert.That(ok, Is.True);
            Assert.That(newZ, Is.EqualTo(2));

            _map.AddStatic(11, 12, new StaticTile(0x0015, 0, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 12, 0), Direction.East, out _), Is.False);
        }

        [Test]
        public void CeilingLowStatic_BlocksPersonHeight16()
        {
            _map.AddStatic(11, 10, new StaticTile(0x0014, 15, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 10, 0), Direction.East, out _), Is.False);

            _map.AddStatic(11, 12, new StaticTile(0x0014, 16, 0));
            Assert.That(_check.CheckMovement(new Point3D(10, 12, 0), Direction.East, out int newZ), Is.True);
            Assert.That(newZ, Is.EqualTo(0));
        }

        [Test]
        public void DiagonalCorner_PlayerRequiresBothSides_GmRequiresAtLeastOne()
        {
            var start = new Point3D(10, 10, 0);
            _map.AddStatic(10, 9, new StaticTile(0x0013, 0, 0));

            Assert.That(_check.CheckMovement(start, Direction.Right, MoverState.Player(), out _), Is.False);
            Assert.That(_check.CheckMovement(start, Direction.Right, MoverState.GameMaster(), out int gmZ), Is.True);
            Assert.That(gmZ, Is.EqualTo(0));

            _map.AddStatic(11, 10, new StaticTile(0x0013, 0, 0));
            Assert.That(_check.CheckMovement(start, Direction.Right, MoverState.GameMaster(), out _), Is.False);
        }

        [Test]
        public void ZeroAllocations_InSteadyState()
        {
            var start = new Point3D(10, 10, 0);
            _map.AddStatic(11, 10, new StaticTile(0x0010, 0, 0));
            for (int i = 0; i < 16; i++)
            {
                _check.CheckMovement(start, Direction.East, out _);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                _check.CheckMovement(start, Direction.East, out _);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void NoSupportAtStart_PreservesLocZ_OnFailure()
        {
            SetLandBlock(10, 10, 15);
            _map.AddStatic(10, 9, new StaticTile(0x0013, 0, 0));

            bool ok = _check.CheckMovement(new Point3D(10, 10, 10), Direction.North, out int newZ);

            Assert.That(ok, Is.False);
            Assert.That(newZ, Is.EqualTo(10));
        }

        [Test]
        public void TallBridgeOrStairs_AllowsClimbAboveStepHeight()
        {
            SetLandBlock(10, 10, 10);
            SetLandBlock(9, 10, 10);
            _tiles.SetItem(0x0016, TileFlags.Surface | TileFlags.Bridge, 16, "tallStair");
            _map.AddStatic(9, 10, new StaticTile(0x0016, 10, 0));

            bool ok = _check.CheckMovement(new Point3D(10, 10, 10), Direction.West, out int newZ);

            Assert.That(ok, Is.True);
            Assert.That(newZ, Is.EqualTo(18));
        }

        [Test]
        public void SubterraneanStatic_UnderLandCenter_IsIgnored()
        {
            SetLandBlock(10, 10, 9);
            SetLandBlock(11, 10, 10);
            _map.AddStatic(11, 10, new StaticTile(0x0010, 7, 0));

            bool ok = _check.CheckMovement(new Point3D(10, 10, 9), Direction.East, out int newZ);

            Assert.That(ok, Is.True);
            Assert.That(newZ, Is.EqualTo(10));
        }

        [Test]
        public void StaticTileWithDoorFlag_StillBlocksGhost()
        {
            _tiles.SetItem(0x0017, TileFlags.Impassable | TileFlags.Door, 20, "staticDoor");
            _map.AddStatic(11, 10, new StaticTile(0x0017, 0, 0));

            bool ok = _check.CheckMovement(new Point3D(10, 10, 0), Direction.East, MoverState.Ghost(), out _);

            Assert.That(ok, Is.False);
        }

        private void SetLandBlock(int x, int y, int z)
        {
            var tile = new LandTile(3, z);
            _map.SetLand(x, y, tile);
            _map.SetLand(x + 1, y, tile);
            _map.SetLand(x, y + 1, tile);
            _map.SetLand(x + 1, y + 1, tile);
        }
    }
}
