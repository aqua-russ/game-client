using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Multis;
using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Учёт динамических сущностей в <see cref="MovementCheck"/>: двери (0x692..0x6F6 и флаг Door), переносимые предметы,
    /// дома/корабли (multi) с пропуском частей без флагов (MultiData.cs) и блокировка живыми мобами (Mobile.CanMove).
    /// </summary>
    public sealed class MovementCheckEntityTests
    {
        private FakeMap _map = null!;
        private FakeTiles _tiles = null!;
        private FakeMultis _multis = null!;
        private EntityStore _entities = null!;
        private MovementCheck _check = null!;

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(64, 64, new LandTile(3, 0));
            _tiles = new FakeTiles();
            _multis = new FakeMultis();
            _entities = new EntityStore();
            _entities.ChangeMap(0);
            _tiles.SetLand(3, TileFlags.None, "grass");
            _tiles.SetItem(0x0675, TileFlags.Door | TileFlags.Impassable | TileFlags.Wall, 20, "door");
            _tiles.SetItem(0x0692, TileFlags.Impassable | TileFlags.Wall, 20, "special door");
            _tiles.SetItem(0x0E40, TileFlags.Impassable | TileFlags.Surface, 4, "chest");
            _tiles.SetItem(0x0001, TileFlags.Surface, 0, "floor");
            _tiles.SetItem(0x0002, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            _check = new MovementCheck(_map, _tiles, _multis, _entities);
        }

        [Test]
        public void ClosedDoor_BlocksPlayer_AllowsGhostAndBody03DB()
        {
            var start = new Point3D(10, 10, 0);
            _entities.PlaceInWorld(new Serial(0x40000100), new ItemInfo(0x0675, 1, new Point3D(11, 10, 0), 0, 0, 0));
            _entities.PlaceInWorld(new Serial(0x40000101), new ItemInfo(0x0692, 1, new Point3D(10, 11, 0), 0, 0, 0));

            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Player(), out _), Is.False);
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Ghost(), out _), Is.True);

            _entities.UpdateMobile(new Serial(0x00000001), new MobileInfo(0x03DB, start, Direction.East, 0, 0, 1));
            var seer = MoverState.FromMobile(_entities.FindMobile(new Serial(0x00000001))!);
            Assert.That(_check.CheckMovement(start, Direction.East, seer, out _), Is.True);
            Assert.That(_check.CheckMovement(start, Direction.South, MoverState.Player(), out _), Is.False);
            Assert.That(_check.CheckMovement(start, Direction.South, MoverState.Ghost(), out _), Is.True);
        }

        [Test]
        public void MovableImpassableItem_BlocksPlayer_AllowsGhost()
        {
            var start = new Point3D(10, 10, 0);
            _entities.PlaceInWorld(new Serial(0x40000200), new ItemInfo(0x0E40, 1, new Point3D(11, 10, 0), 0, 0x20, 0));

            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Player(), out _), Is.False);
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Ghost(), out _), Is.True);
        }

        [Test]
        public void MovableSurfaceItem_IsNotStoodOn_ButBlocksLand()
        {
            _tiles.SetItem(0x0E3C, TileFlags.Surface, 2, "crate");
            _entities.PlaceInWorld(new Serial(0x40000300), new ItemInfo(0x0E3C, 1, new Point3D(11, 10, 0), 0, 0, 0));
            _entities.PlaceInWorld(new Serial(0x40000301), new ItemInfo(0x0E3C, 1, new Point3D(11, 12, 0), 0, 0x20, 0));

            Assert.That(_check.CheckMovement(new Point3D(10, 10, 0), Direction.East, MoverState.Player(), out int fixedZ), Is.True);
            Assert.That(fixedZ, Is.EqualTo(2));
            Assert.That(_check.CheckMovement(new Point3D(10, 12, 0), Direction.East, MoverState.Player(), out _), Is.False);
        }

        [Test]
        public void ZeroAllocations_WithItemsMultisAndMobiles()
        {
            var start = new Point3D(10, 10, 0);
            _multis.Set(0x0010, new MultiComponent(0x0001, 0, 0, 0, TileFlags.Surface), new MultiComponent(0x0002, 3, 0, 0, TileFlags.Wall));
            _entities.PlaceInWorld(new Serial(0x40000400), new ItemInfo(0x0010, 1, new Point3D(12, 12, 0), 0, 0, 0, isMulti: true));
            _entities.PlaceInWorld(new Serial(0x40000401), new ItemInfo(0x0E40, 1, new Point3D(11, 11, 0), 0, 0x20, 0));
            _entities.UpdateMobile(new Serial(0x00000030), new MobileInfo(0x00EE, new Point3D(9, 10, 0), Direction.West, 0, 0, 1));
            for (int i = 0; i < 16; i++)
            {
                _check.CheckMovement(start, (Direction)(i & 7), MoverState.Player(), out _);
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                _check.CheckMovement(start, (Direction)(i & 7), MoverState.Player(), out _);
            }

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void MultiComponents_FloorAndWall_ZeroFlagComponentIgnored()
        {
            _multis.Set(0x64,
                new MultiComponent(0x0001, 0, 0, 0, TileFlags.Surface),
                new MultiComponent(0x0002, 1, 0, 0, TileFlags.Impassable | TileFlags.Wall),
                new MultiComponent(0x0002, 0, 1, 0, TileFlags.None));
            _entities.PlaceInWorld(new Serial(0x40000300), new ItemInfo(0x64, 1, new Point3D(11, 10, 0), 0, 0, 0, isMulti: true));

            var start = new Point3D(10, 10, 0);
            Assert.That(_check.CheckMovement(start, Direction.East, out int floorZ), Is.True);
            Assert.That(floorZ, Is.EqualTo(0));
            Assert.That(_check.CheckMovement(new Point3D(11, 10, 0), Direction.East, out _), Is.False);
            Assert.That(_check.CheckMovement(new Point3D(11, 10, 0), Direction.South, out _), Is.True);
        }

        [Test]
        public void MobileOnTargetCell_BlocksLivingPlayerWithoutShove()
        {
            var start = new Point3D(10, 10, 0);
            var playerSerial = new Serial(0x00000010);
            var ratSerial = new Serial(0x00000020);
            _entities.UpdateMobile(playerSerial, new MobileInfo(0x0190, start, Direction.East, 0, 0, 1));
            _entities.UpdateMobile(ratSerial, new MobileInfo(0x00EE, new Point3D(11, 10, 0), Direction.West, 0, 0, 1));

            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Player(playerSerial.Value, canShoveMobiles: false), out int blockedZ), Is.False);
            Assert.That(blockedZ, Is.EqualTo(0));
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Player(playerSerial.Value, canShoveMobiles: true), out _), Is.True);
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.GameMaster(playerSerial.Value), out _), Is.True);
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Ghost(playerSerial.Value), out _), Is.True);
        }

        [Test]
        public void GhostDeadBondedOrFarZMobile_DoesNotBlockLivingPlayer()
        {
            var start = new Point3D(10, 10, 0);
            var blocker = new Serial(0x00000030);
            _entities.UpdateMobile(blocker, new MobileInfo(0x0192, new Point3D(11, 10, 0), Direction.North, 0, 0, 1));
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Player(), out _), Is.True);

            _entities.UpdateMobile(blocker, new MobileInfo(0x00EE, new Point3D(11, 10, 0), Direction.North, 0, 0, 1));
            new MobileStatusUpdates(_entities).OnBonded(blocker, true);
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Player(), out _), Is.True);

            new MobileStatusUpdates(_entities).OnBonded(blocker, false);
            _entities.UpdateMobile(blocker, new MobileInfo(0x00EE, new Point3D(11, 10, 15), Direction.North, 0, 0, 1));
            Assert.That(_check.CheckMovement(start, Direction.East, MoverState.Player(), out _), Is.True);
        }
    }
}
