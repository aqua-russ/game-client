using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Показ шагов своего персонажа (PLAN Стадия 3, п. 9; ADR 0020 пп. 1, 3, 5, 6). Клетку предсказывает
    /// <see cref="PlayerMotion"/>; картинка едет из прежней клетки в предсказанную за время шага по таблице «пешком или
    /// верхом × шаг или бег» (StepTiming; сервер: 400 / 200 мс пешком, 200 / 100 мс верхом — TARGET.md §3). Шаг начинается в
    /// тике, где он предсказан, а если картинка ещё показывает прежний шаг — сразу после него: отставание не копится.
    /// Откат по отказу (0x21), место от сервера (0x20) и вход в мир — перескок на клетку сервера без езды (ADR 0020 пп. 3, 5).
    /// </summary>
    public sealed class PlayerStepShowTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(20, 20, 0);
        private static readonly Point3D East1 = new Point3D(21, 20, 0);
        private static readonly Point3D East2 = new Point3D(22, 20, 0);

        private EntityStore _world = null!;
        private FakeClock _clock = null!;
        private PlayerMotion _motion = null!;
        private PlayerStepShow _show = null!;
        private List<MotionSnap> _snaps = null!;
        private byte _sequence;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(128, 128, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            _world = new EntityStore();
            _world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(128, 128)));
            _world.ChangeMap(0);
            _clock = new FakeClock();
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), new FakeMovementSender());
            _show = new PlayerStepShow(_motion, _clock);
            _snaps = new List<MotionSnap>();
            _show.Snapped += snap => _snaps.Add(snap);
            _sequence = 0;
        }

        [TearDown]
        public void TearDown() => _show.Dispose();

        [Test]
        public void BeforeWorld_NothingToShow()
        {
            Assert.That(_show.IsPlaced, Is.False);
            Assert.That(_snaps, Is.Empty);
        }

        [Test]
        public void EnterWorld_StandsOnServerCell_WithoutRide()
        {
            _clock.NowMs = 500;
            _motion.EnterWorld(Start, Direction.East);

            Assert.That(_show.IsPlaced, Is.True);
            AssertStanding(Start);
            Assert.That(_snaps, Has.Count.EqualTo(1));
            Assert.That(_snaps[0].Cell, Is.EqualTo(Start));
            Assert.That(_snaps[0].Reason, Is.EqualTo(SnapReason.Appeared));
            Assert.That(_snaps[0].AtMs, Is.EqualTo(500));
        }

        [Test]
        public void CreatedAfterEnterWorld_StandsOnPredictedCell()
        {
            _show.Dispose();
            _motion.EnterWorld(Start, Direction.East);
            _motion.Step(Direction.East, true);

            _show = new PlayerStepShow(_motion, _clock);

            Assert.That(_show.IsPlaced, Is.True);
            AssertStanding(East1);
        }

        [TestCase(false, false, 400)]
        [TestCase(false, true, 200)]
        [TestCase(true, false, 200)]
        [TestCase(true, true, 100)]
        public void Step_RidesFromOldCellToPredicted_InStepTime(bool mounted, bool run, int expectedMs)
        {
            if (mounted)
            {
                _world.Equip(new Serial(0x40000010), new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), PlayerSerial, Layer.Mount);
            }

            _motion.EnterWorld(Start, Direction.East);
            StepAt(1000, run);

            var step = _show.Current;
            Assert.That(step.From, Is.EqualTo(Start));
            Assert.That(step.To, Is.EqualTo(East1));
            Assert.That(step.StartMs, Is.EqualTo(1000), "картинка трогается в том же тике, без ожидания сервера");
            Assert.That(step.DurationMs, Is.EqualTo(expectedMs));
            Assert.That(step.IsRunning, Is.EqualTo(run));
            Assert.That(step.IsMounted, Is.EqualTo(mounted));
            Assert.That(step.Progress(1000 + expectedMs / 2), Is.EqualTo(0.5));
            Assert.That(step.Progress(1000 + expectedMs), Is.EqualTo(1));
        }

        [Test]
        public void Turn_IsNotStep()
        {
            _motion.EnterWorld(Start, Direction.East);
            _clock.NowMs = 1000;

            Assert.That(_motion.Step(Direction.North, true), Is.EqualTo(StepResult.Turned));
            _show.Update(1000);

            AssertStanding(Start);
        }

        [Test]
        public void NextStepBeforePictureFinished_StartsRightAfterIt()
        {
            _motion.EnterWorld(Start, Direction.East);
            StepAt(1000, true);
            StepAt(1150, true);

            Assert.That(_show.Current.To, Is.EqualTo(East1), "картинка ещё едет первый шаг");
            Assert.That(_show.WaitingSteps, Is.EqualTo(1));

            _show.Update(1200);

            var step = _show.Current;
            Assert.That(step.From, Is.EqualTo(East1));
            Assert.That(step.To, Is.EqualTo(East2));
            Assert.That(step.StartMs, Is.EqualTo(1200), "сразу после конца первого, не раньше");
            Assert.That(step.DurationMs, Is.EqualTo(200), "отставание в один шаг — обычное время");
            Assert.That(_show.WaitingSteps, Is.EqualTo(0));
        }

        [Test]
        public void StepAfterPause_StartsWhenPredicted()
        {
            _motion.EnterWorld(Start, Direction.East);
            StepAt(1000, false);
            _show.Update(2000);

            StepAt(2500, false);

            Assert.That(_show.Current.From, Is.EqualTo(East1));
            Assert.That(_show.Current.StartMs, Is.EqualTo(2500));
            Assert.That(_show.Current.DurationMs, Is.EqualTo(400));
        }

        [Test]
        public void Reject_SnapsToServerCell_AtOnce_WaitingDropped()
        {
            _clock.NowMs = 900;
            _motion.EnterWorld(Start, Direction.East);
            StepAt(1000, true);
            StepAt(1150, true);
            _clock.NowMs = 1180;

            Assert.That(_motion.Reject(0, Start, Direction.East), Is.True);

            AssertStanding(Start);
            Assert.That(_show.WaitingSteps, Is.EqualTo(0));
            Assert.That(_snaps, Has.Count.EqualTo(2));
            Assert.That(_snaps[1].Cell, Is.EqualTo(Start));
            Assert.That(_snaps[1].Reason, Is.EqualTo(SnapReason.Rejected));
            Assert.That(_snaps[1].DroppedSteps, Is.EqualTo(1));
            Assert.That(_snaps[1].AtMs, Is.EqualTo(1180));
        }

        [Test]
        public void RejectThenStepInSameTick_RidesFromServerCell()
        {
            _motion.EnterWorld(Start, Direction.East);
            StepAt(1000, true);
            _clock.NowMs = 1100;
            _motion.Reject(0, Start, Direction.East);

            StepAt(1100, true);

            Assert.That(_show.Current.From, Is.EqualTo(Start));
            Assert.That(_show.Current.To, Is.EqualTo(East1));
            Assert.That(_show.Current.StartMs, Is.EqualTo(1100));
        }

        [Test]
        public void ServerPosition_SnapsAsRepositioned()
        {
            _motion.EnterWorld(Start, Direction.East);
            StepAt(1000, true);
            var there = new Point3D(60, 70, 10);
            _clock.NowMs = 1050;

            _motion.ResetToServer(there, Direction.South);

            AssertStanding(there);
            Assert.That(_snaps[1].Reason, Is.EqualTo(SnapReason.Repositioned));
            Assert.That(_snaps[1].Cell, Is.EqualTo(there));
        }

        [Test]
        public void StaleReject_KeepsRide()
        {
            _motion.EnterWorld(Start, Direction.East);
            StepAt(1000, true);
            StepAt(1200, true);
            _motion.Reject(0, Start, Direction.East);
            StepAt(1300, true);

            _motion.Reject(1, Start, Direction.East);

            Assert.That(_show.Current.To, Is.EqualTo(East1), "старый отказ не трогает картинку");
            Assert.That(_snaps, Has.Count.EqualTo(2), "вход и один настоящий откат");
        }

        [Test]
        public void AfterDispose_IgnoresMotion()
        {
            _motion.EnterWorld(Start, Direction.East);
            _show.Dispose();

            StepAt(1000, true);
            _motion.Reject(0, Start, Direction.North);

            AssertStanding(Start);
            Assert.That(_snaps, Has.Count.EqualTo(1));
        }

        [Test]
        public void SteadyWalking_NoNewMemory()
        {
            _motion.EnterWorld(Start, Direction.East);
            Walk(1000, 8);

            Assert.That(Walk(3000, 8), Is.EqualTo(0), "шаг и тик показа без новой памяти");
        }

        // Шаг на восток в момент now (как StepPacer в тике) и тик показа; сервер подтверждает сразу, чтобы очередь не кончалась.
        private void StepAt(long now, bool run)
        {
            _clock.NowMs = now;
            Assert.That(_motion.Step(Direction.East, run), Is.EqualTo(StepResult.Moved));
            _show.Update(now);
        }

        // Бег на восток по шагу в 200 мс; сервер подтверждает каждый шаг (номера с 0 после входа). Возвращает, сколько памяти
        // взяли шаг и тик показа (подтверждение пишет мир клиента и в счёт не идёт).
        private long Walk(long from, int steps)
        {
            long allocated = 0;
            for (int i = 0; i < steps; i++)
            {
                long now = from + i * 200L;
                _clock.NowMs = now;
                long before = GC.GetAllocatedBytesForCurrentThread();
                _motion.Step(Direction.East, true);
                _show.Update(now);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                _motion.Confirm(_sequence++);
            }

            return allocated;
        }

        private void AssertStanding(Point3D cell)
        {
            var step = _show.Current;
            Assert.That(step.From, Is.EqualTo(cell));
            Assert.That(step.To, Is.EqualTo(cell));
            Assert.That(step.DurationMs, Is.EqualTo(0));
        }
    }
}
