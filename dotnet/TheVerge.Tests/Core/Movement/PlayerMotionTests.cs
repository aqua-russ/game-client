using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Состояние движения своего персонажа (PLAN Стадия 3, п. 4) по правилам сервера: шаг в новую сторону только
    /// поворачивает (Projects/Server/Mobiles/Mobile.cs, Move: сравнение направлений без бита бега), поворот тоже получает
    /// номер и подтверждение; шаг в ту же сторону проверяется MovementCheck и сразу ставит персонажа на новую клетку;
    /// номера шагов и предел неподтверждённых — MovementThrottle.cs и ServerLimits; отказ 0x21 несёт клетку и направление
    /// сервера (OutgoingMovementPackets.SendMovementRej) и сбрасывает номер в 0. В мир клиента (EntityStore) идёт только
    /// клетка, которую подтвердил сервер.
    /// </summary>
    public sealed class PlayerMotionTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(10, 10, 0);

        private FakeMap _map = null!;
        private EntityStore _world = null!;
        private FakeMovementSender _sender = null!;
        private PlayerMotion _motion = null!;

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(64, 64, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            tiles.SetItem(0x0010, TileFlags.Surface, 2, "step2");
            tiles.SetItem(0x0013, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            _world = new EntityStore();
            _world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(64, 64)));
            _world.ChangeMap(0);
            _sender = new FakeMovementSender();
            _motion = new PlayerMotion(_world, new MovementCheck(_map, tiles, null, _world), _sender);
        }

        [Test]
        public void BeforeServerPlacedPlayer_StepDoesNothing()
        {
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.NotInWorld));

            var emptyWorld = new EntityStore();
            var orphan = new PlayerMotion(emptyWorld, new MovementCheck(_map, new FakeTiles(), null, emptyWorld), _sender);
            orphan.ResetToServer(Start, Direction.East);
            Assert.That(orphan.Step(Direction.East, true), Is.EqualTo(StepResult.NotInWorld), "в мире нет своего персонажа");

            Assert.That(_sender.Steps, Is.Empty);
        }

        [Test]
        public void NewDirection_OnlyTurns_ThenSameDirectionMoves()
        {
            _motion.ResetToServer(Start, Direction.North);

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Turned));
            Assert.That(_motion.Position, Is.EqualTo(Start));
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.East));
            Assert.That(_motion.BodyDirection, Is.EqualTo(Direction.East), "тело смотрит туда же, пока нет трека F");
            Assert.That(_sender.Last, Is.EqualTo(new FakeMovementSender.Sent(Direction.East, true, 0)));
            Assert.That(_motion.PendingSteps, Is.EqualTo(1), "поворот тоже ждёт подтверждения");

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(11, 10, 0)));
            Assert.That(_sender.Last, Is.EqualTo(new FakeMovementSender.Sent(Direction.East, true, 1)));
        }

        [Test]
        public void SameDirection_MovesAtOnce_WithZFromMovementCheck()
        {
            _map.AddStatic(11, 10, new StaticTile(0x0010, 0, 0));
            _motion.ResetToServer(Start, Direction.East);

            Assert.That(_motion.Step(Direction.East, false), Is.EqualTo(StepResult.Moved));
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(11, 10, 2)), "ступенька высотой 2");
            Assert.That(_motion.IsRunning, Is.False);
            Assert.That(_sender.Last, Is.EqualTo(new FakeMovementSender.Sent(Direction.East, false, 0)));

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(12, 10, 0)));
            Assert.That(_motion.IsRunning, Is.True);
            Assert.That(_sender.Last, Is.EqualTo(new FakeMovementSender.Sent(Direction.East, true, 1)));
            Assert.That(_world.Player!.Position, Is.EqualTo(Start), "мир клиента ждёт подтверждения сервера");
        }

        [Test]
        public void RunningBitInDirection_IsNotATurn()
        {
            _motion.ResetToServer(Start, Direction.East | Direction.Running);

            Assert.That(_motion.Step(Direction.East | Direction.Running, false), Is.EqualTo(StepResult.Moved));
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.East));
            Assert.That(_sender.Last.Direction, Is.EqualTo(Direction.East), "бег идёт отдельным флагом");
        }

        [Test]
        public void Wall_NothingSent_SequenceNotSpent()
        {
            _map.AddStatic(11, 10, new StaticTile(0x0013, 0, 0));
            _motion.ResetToServer(Start, Direction.East);

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Blocked));
            Assert.That(_motion.Position, Is.EqualTo(Start));
            Assert.That(_sender.Steps, Is.Empty);

            Assert.That(_motion.Step(Direction.South, true), Is.EqualTo(StepResult.Turned));
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0));
        }

        [Test]
        public void UnconfirmedLimit_FurtherStepsAndTurnsWait()
        {
            _motion.ResetToServer(Start, Direction.East);
            for (int i = 0; i < ServerLimits.MaxUnacknowledgedSteps; i++)
            {
                Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved), "шаг " + i);
            }

            var reached = _motion.Position;
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.QueueFull));
            Assert.That(_motion.Step(Direction.North, true), Is.EqualTo(StepResult.QueueFull));
            Assert.That(_motion.Position, Is.EqualTo(reached));
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.East));
            Assert.That(_sender.Steps.Count, Is.EqualTo(ServerLimits.MaxUnacknowledgedSteps));
        }

        [Test]
        public void Mounted_ByItemOnMountLayer()
        {
            Assert.That(_motion.IsMounted, Is.False);

            var mount = new Serial(0x40000010);
            _world.Equip(mount, new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), PlayerSerial, Layer.Mount);
            Assert.That(_motion.IsMounted, Is.True);

            _world.Remove(mount);
            Assert.That(_motion.IsMounted, Is.False);
        }

        [Test]
        public void Confirm_RemovesOldestStep_WorldGetsConfirmedCell()
        {
            _motion.ResetToServer(Start, Direction.East);
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);

            Assert.That(_motion.Confirm(5), Is.False, "чужой номер");
            Assert.That(_motion.PendingSteps, Is.EqualTo(2));
            Assert.That(_world.Player!.Position, Is.EqualTo(Start));

            Assert.That(_motion.Confirm(0), Is.True);
            Assert.That(_motion.PendingSteps, Is.EqualTo(1));
            Assert.That(_world.Player!.Position, Is.EqualTo(new Point3D(11, 10, 0)));
            Assert.That(_world.Player!.Direction, Is.EqualTo(Direction.East));

            Assert.That(_motion.Confirm(1), Is.True);
            Assert.That(_world.Player!.Position, Is.EqualTo(new Point3D(12, 10, 0)));
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(12, 10, 0)));
        }

        [Test]
        public void ResetToServer_StandsOnServerCell_ClearsQueue_SequenceZero()
        {
            _motion.ResetToServer(Start, Direction.East);
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);

            var server = new Point3D(20, 20, 5);
            _motion.ResetToServer(server, Direction.West | Direction.Running);

            Assert.That(_motion.Position, Is.EqualTo(server));
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.West));
            Assert.That(_motion.BodyDirection, Is.EqualTo(Direction.West));
            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            Assert.That(_world.Player!.Position, Is.EqualTo(server), "мир клиента — на клетке сервера");
            Assert.That(_world.Player!.Direction, Is.EqualTo(Direction.West));

            Assert.That(_motion.Step(Direction.West, false), Is.EqualTo(StepResult.Moved));
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0), "после отката номер снова 0");
        }

        [Test]
        public void StepAndConfirm_DoNotAllocate()
        {
            _motion.ResetToServer(Start, Direction.East);
            WalkBackAndForth(16);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            WalkBackAndForth(200);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private void WalkBackAndForth(int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                var direction = (i & 1) == 0 ? Direction.East : Direction.West;
                _motion.Step(direction, true);
                _motion.Confirm(_sender.Last.Sequence);
                _motion.Step(direction, true);
                _motion.Confirm(_sender.Last.Sequence);
                _sender.Steps.Clear();
            }
        }
    }
}
