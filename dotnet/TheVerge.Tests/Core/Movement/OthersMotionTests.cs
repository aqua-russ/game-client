using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using static TheVerge.Tests.Core.Movement.OthersScene;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Чужие мобы (PLAN Стадия 3, п. 8; ADR 0020 пп. 4–5). По коду сервера (Projects/Server/Mobiles/Mobile.cs): шаг — 0x77 с
    /// новой клеткой (Move → SendMobileMovingUsingCache всем, кто видит); поворот на месте — тот же 0x77 с прежней клеткой
    /// (Move: newLocation = oldLocation), а смена флагов, «злости» и направления без шага — 0x77 с текущей клеткой
    /// (ProcessDelta); новый зритель получает 0x78 и следом 0x77 той же клетки (SetLocation, затем Move); телепорт — 0x78
    /// с новой клеткой (SetLocation с isTeleport); ушедшему из обзора — 0x1D. Показ шага — без буфера задержки, длительность
    /// по таблице «пешком или верхом × шаг или бег» (бит бега — 0x80 в направлении 0x77; NPC сервер ставит его под эту
    /// таблицу: Projects/UOContent/Mobiles/AI/BaseAI/AIMovement.cs, ShouldRun).
    /// </summary>
    public sealed class OthersMotionTests
    {
        private OthersScene _s = null!;

        [SetUp]
        public void SetUp() => _s = new OthersScene();

        [TearDown]
        public void TearDown() => _s.Dispose();

        [Test]
        public void Appeared_StandsOnCell_NoStep()
        {
            _s.At(1000);
            _s.Appear(Orc, 200, 200, 5);
            _s.Tick(1000);

            var step = _s.Step(Orc);
            Assert.That(step.From, Is.EqualTo(new Point3D(200, 200, 5)));
            Assert.That(step.To, Is.EqualTo(new Point3D(200, 200, 5)));
            Assert.That(step.DurationMs, Is.EqualTo(0));
            Assert.That(_s.Started, Is.Empty, "появление — не шаг");
            Assert.That(_s.Snaps, Has.Count.EqualTo(1));
            Assert.That(_s.Snaps[0].Reason, Is.EqualTo(SnapReason.Appeared));
            Assert.That(_s.Motion.SnapCount, Is.EqualTo(0), "появление в перескоки не считается");
            Assert.That(_s.Motion.TrackedCount, Is.EqualTo(1));
        }

        [TestCase(false, false, 400)]
        [TestCase(false, true, 200)]
        [TestCase(true, false, 200)]
        [TestCase(true, true, 100)]
        public void Step_FromStanding_StartsOnArrival_TableDuration(bool mounted, bool running, int expectedMs)
        {
            _s.Appear(Orc, 200, 200);
            if (mounted)
            {
                _s.Mount(Orc);
            }

            _s.Tick(1000);
            _s.At(1500);
            _s.Move(Orc, 201, 200, Dir(Direction.East, running), 3);
            _s.Tick(1516);

            var step = _s.Step(Orc);
            Assert.That(step.From, Is.EqualTo(new Point3D(200, 200, 0)));
            Assert.That(step.To, Is.EqualTo(new Point3D(201, 200, 3)));
            Assert.That(step.Facing, Is.EqualTo(Dir(Direction.East, running)));
            Assert.That(step.IsRunning, Is.EqualTo(running));
            Assert.That(step.IsMounted, Is.EqualTo(mounted));
            Assert.That(step.ArrivedMs, Is.EqualTo(1500));
            Assert.That(step.StartMs, Is.EqualTo(1500), "без буфера задержки: с прихода пакета, а не со следующего тика");
            Assert.That(step.DurationMs, Is.EqualTo(expectedMs));
            Assert.That(step.Behind, Is.EqualTo(1));
            Assert.That(step.IsCatchUp, Is.False);
            Assert.That(_s.Started, Has.Count.EqualTo(1));
        }

        [Test]
        public void SameCell_TurnOrFlags_IsNotStep()
        {
            _s.Appear(Orc, 200, 200);
            _s.Move(Orc, 200, 200, Direction.South);
            _s.Tick(1000);
            _s.Move(Orc, 200, 200, Direction.West);
            _s.Move(Orc, 200, 200, Direction.West | Direction.Running);
            _s.Tick(1100);

            Assert.That(_s.Started, Is.Empty, "0x77 той же клетки — поворот или флаги, не шаг");
            Assert.That(_s.Motion.WaitingSteps(Orc), Is.EqualTo(0));
            Assert.That(_s.Snaps, Has.Count.EqualTo(1));
        }

        [Test]
        public void TurnDuringStep_DoesNotAddStep()
        {
            _s.Appear(Orc, 200, 200);
            _s.Tick(0);
            _s.Move(Orc, 201, 200, Direction.East);
            _s.Tick(0);
            _s.At(100);
            _s.Move(Orc, 201, 200, Direction.North);
            _s.Tick(500);

            Assert.That(_s.Started, Has.Count.EqualTo(1));
            Assert.That(_s.Step(Orc).To, Is.EqualTo(new Point3D(201, 200, 0)));
        }

        [Test]
        public void SecondStep_WhileShowing_StartsExactlyAtEnd()
        {
            _s.Appear(Orc, 200, 200);
            _s.Tick(0);
            _s.Move(Orc, 201, 200, Dir(Direction.East, true));
            _s.Tick(0);
            _s.At(150);
            _s.Move(Orc, 202, 200, Dir(Direction.East, true));
            _s.Tick(150);
            Assert.That(_s.Motion.WaitingSteps(Orc), Is.EqualTo(1));
            _s.Tick(199);
            Assert.That(_s.Started, Has.Count.EqualTo(1), "первый шаг ещё показывается");

            _s.Tick(208);

            var step = _s.Step(Orc);
            Assert.That(_s.Started, Has.Count.EqualTo(2));
            Assert.That(step.From, Is.EqualTo(new Point3D(201, 200, 0)));
            Assert.That(step.To, Is.EqualTo(new Point3D(202, 200, 0)));
            Assert.That(step.ArrivedMs, Is.EqualTo(150));
            Assert.That(step.StartMs, Is.EqualTo(200), "ровно в конце первого: без паузы");
            Assert.That(step.DurationMs, Is.EqualTo(200), "без ускорения: картинка не отстала");
            Assert.That(_s.Motion.WaitingSteps(Orc), Is.EqualTo(0));
        }

        [Test]
        public void StepAfterIdle_StartsOnArrival_NotAtOldEnd()
        {
            _s.Appear(Orc, 200, 200);
            _s.Tick(0);
            _s.Move(Orc, 201, 200, Dir(Direction.East, true));
            _s.Tick(0);
            _s.At(350);
            _s.Move(Orc, 202, 200, Dir(Direction.East, true));
            _s.Tick(366);

            Assert.That(_s.Step(Orc).StartMs, Is.EqualTo(350));
        }

        [Test]
        public void Jump_MoreThanTwoCells_Snaps_TwoCells_IsStep()
        {
            _s.Appear(Orc, 200, 200);
            _s.Tick(0);
            _s.At(100);
            _s.Move(Orc, 203, 200, Direction.East);
            _s.Tick(100);

            Assert.That(_s.Started, Is.Empty);
            Assert.That(_s.Snaps[^1].Reason, Is.EqualTo(SnapReason.Jumped));
            Assert.That(_s.Snaps[^1].Cell, Is.EqualTo(new Point3D(203, 200, 0)));
            Assert.That(_s.Snaps[^1].AtMs, Is.EqualTo(100));
            Assert.That(_s.Step(Orc).To, Is.EqualTo(new Point3D(203, 200, 0)));
            Assert.That(_s.Step(Orc).DurationMs, Is.EqualTo(0));
            Assert.That(_s.Motion.SnapCount, Is.EqualTo(1));

            _s.Move(Orc, 205, 201, Direction.East);
            _s.Tick(100);
            Assert.That(_s.Started, Has.Count.EqualTo(1), "2 клетки — едет");
            Assert.That(_s.Step(Orc).From, Is.EqualTo(new Point3D(203, 200, 0)));

            _s.Move(Orc, 205, 201, Direction.East, 20);
            Assert.That(_s.Snaps[^1].Reason, Is.EqualTo(SnapReason.Jumped), "та же клетка с другой высотой — не шаг, а перескок");
        }

        [Test]
        public void Teleport_KnownMobileNewCell_Snaps_DropsWaiting_SameCellNothing()
        {
            _s.Appear(Orc, 200, 200);
            _s.Tick(0);
            _s.Move(Orc, 201, 200, Direction.East);
            _s.Tick(0);
            _s.Move(Orc, 202, 200, Direction.East);
            _s.At(50);
            _s.Incoming(Orc, 210, 205);

            Assert.That(_s.Snaps[^1].Reason, Is.EqualTo(SnapReason.Teleported));
            Assert.That(_s.Snaps[^1].DroppedSteps, Is.EqualTo(1));
            Assert.That(_s.Motion.WaitingSteps(Orc), Is.EqualTo(0));
            _s.Tick(1000);
            Assert.That(_s.Started, Has.Count.EqualTo(1), "выброшенный шаг не показывается");
            Assert.That(_s.Step(Orc).From, Is.EqualTo(new Point3D(210, 205, 0)));
            Assert.That(_s.Step(Orc).To, Is.EqualTo(new Point3D(210, 205, 0)));
            Assert.That(_s.Motion.SnapCount, Is.EqualTo(1));

            _s.Incoming(Orc, 210, 205);
            Assert.That(_s.Snaps, Has.Count.EqualTo(2), "0x78 той же клетки (тело, цвет) — не перескок");
        }

        [Test]
        public void Removed_Forgotten_Cleared_AllForgotten_ThenAppearAgain()
        {
            _s.Appear(Orc, 200, 200);
            _s.Appear(Rat, 205, 205);
            _s.Store.Remove(Orc);
            Assert.That(_s.Motion.TryGetStep(Orc, out _), Is.False);
            Assert.That(_s.Motion.TrackedCount, Is.EqualTo(1));

            _s.Store.ChangeMap(0);
            _s.Store.ChangeMap(1);
            Assert.That(_s.Motion.TrackedCount, Is.EqualTo(0), "смена карты: сервер пришлёт всех заново");

            _s.Appear(Rat, 205, 205);
            Assert.That(_s.Snaps[^1].Reason, Is.EqualTo(SnapReason.Appeared));
        }

        [Test]
        public void UnknownMobileMoving_Appears()
        {
            _s.Move(Orc, 200, 200, Direction.East);

            Assert.That(_s.Snaps[^1].Reason, Is.EqualTo(SnapReason.Appeared));
            Assert.That(_s.Step(Orc).To, Is.EqualTo(new Point3D(200, 200, 0)));
        }

        [Test]
        public void OwnPlayer_NotTracked()
        {
            var player = _s.Store.PlayerSerial;
            _s.Motion.OnIncoming(player);
            _s.Motion.OnMoving(player);

            Assert.That(_s.Motion.TryGetStep(player, out _), Is.False, "свой персонаж — PlayerMotion");
            Assert.That(_s.Motion.TrackedCount, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_StopsListeningToWorld()
        {
            _s.Appear(Orc, 200, 200);
            _s.Motion.Dispose();
            _s.Store.Remove(Orc);

            Assert.That(_s.Motion.TryGetStep(Orc, out _), Is.True, "после Dispose события мира не доходят");
        }

        [Test]
        public void Progress_FromZeroToOne()
        {
            var step = new ShownStep(new StepMove(new Point3D(1, 1, 0), new Point3D(2, 1, 0), Direction.East, false), 1000, 1000, 200, 1);
            Assert.That(step.Progress(900), Is.EqualTo(0));
            Assert.That(step.Progress(1000), Is.EqualTo(0));
            Assert.That(step.Progress(1100), Is.EqualTo(0.5));
            Assert.That(step.Progress(1200), Is.EqualTo(1));
            Assert.That(step.Progress(5000), Is.EqualTo(1));
            var standing = new ShownStep(new StepMove(step.To, step.To, Direction.East, false), 0, 1000, 0, 0);
            Assert.That(standing.Progress(1000), Is.EqualTo(1), "стоит — уже на месте");
        }
    }
}
