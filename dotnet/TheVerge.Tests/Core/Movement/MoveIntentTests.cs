using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Намерение движения из клавиш (PLAN Стадия 3, п. 1; CONTROLS §4): бег по умолчанию, `~` переключает, Shift на время
    /// удержания инвертирует (effectiveRun = isRunToggleActive ^ isShiftHeld); пара клавиш ждёт около 50 мс перед первым
    /// шагом (docs/TARGET.md §3.1, ClientLimits.MovePairWaitMs); короткий тап даёт шаг; нажатие во время шага запоминается;
    /// без фокуса и в текстовом поле все клавиши отпущены. Когда спрашивать о шаге, решает темп шагов (PLAN 3.7): тест
    /// спрашивает сам в нужные моменты времени.
    /// </summary>
    public sealed class MoveIntentTests
    {
        private const int Wait = ClientLimits.MovePairWaitMs;

        private FakeClock _clock = null!;
        private MoveIntent _intent = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _intent = new MoveIntent(_clock, CameraLayout.NorthUp);
        }

        [Test]
        public void PairWait_IsAbout50Ms()
        {
            Assert.That(ClientLimits.MovePairWaitMs, Is.EqualTo(50));
        }

        [Test]
        public void Run_OnByDefault_TildeToggles_ShiftInvertsWhileHeld()
        {
            Assert.That(_intent.IsRunning, Is.True, "по умолчанию бег");

            _intent.SetShift(true);
            Assert.That(_intent.IsRunning, Is.False, "Shift при беге — шаг");
            _intent.SetShift(false);
            Assert.That(_intent.IsRunning, Is.True);

            _intent.ToggleRun();
            Assert.That(_intent.IsRunning, Is.False, "~ — всегда шаг");
            _intent.SetShift(true);
            Assert.That(_intent.IsRunning, Is.True, "Shift при шаге — бег");
            _intent.SetShift(false);
            _intent.ToggleRun();
            Assert.That(_intent.IsRunning, Is.True, "ещё раз ~ — снова бег");
        }

        [Test]
        public void SingleKeyFromRest_WaitsForSecondKey_ThenSteps()
        {
            _intent.Press(MoveKeys.Up);

            _clock.NowMs = Wait - 1;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "ещё ждём вторую клавишу");

            _clock.NowMs = Wait;
            Assert.That(_intent.TryTakeStep(out var direction, out bool run), Is.True);
            Assert.That(direction, Is.EqualTo(Direction.North));
            Assert.That(run, Is.True);
        }

        [Test]
        public void SecondKeyWithinWait_GivesDiagonalAtOnce()
        {
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = 30;
            _intent.Press(MoveKeys.Right);

            Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True, "пара уже есть — ждать нечего");
            Assert.That(direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        public void ShortTap_GivesOneStep()
        {
            _intent.Press(MoveKeys.Right);
            _clock.NowMs = 20;
            _intent.Release(MoveKeys.Right);
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "до конца ожидания второй клавиши — рано");

            _clock.NowMs = Wait;
            Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True, "тап не потерялся");
            Assert.That(direction, Is.EqualTo(Direction.East));

            _clock.NowMs = 300;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "один тап — один шаг");
        }

        [Test]
        public void HeldKey_StepsRepeat_ReleaseStopsWithoutExtraStep()
        {
            _intent.Press(MoveKeys.Down);
            for (int t = Wait; t <= Wait + 400; t += 200)
            {
                _clock.NowMs = t;
                Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True, "шаг в " + t + " мс");
                Assert.That(direction, Is.EqualTo(Direction.South));
            }

            _clock.NowMs = 500;
            _intent.Release(MoveKeys.Down);
            _clock.NowMs = 650;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "отпустил — стоим");
        }

        [Test]
        public void PressBetweenSteps_IsRemembered_ThenHeldKeysAgain()
        {
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = Wait;
            _intent.TryTakeStep(out _, out _);

            _clock.NowMs = 100;
            _intent.Press(MoveKeys.Right);
            _clock.NowMs = 120;
            _intent.Release(MoveKeys.Right);

            _clock.NowMs = 250;
            Assert.That(_intent.TryTakeStep(out var remembered, out _), Is.True);
            Assert.That(remembered, Is.EqualTo(Direction.Right), "направление в момент нажатия: W+D");
            _clock.NowMs = 450;
            Assert.That(_intent.TryTakeStep(out var held, out _), Is.True);
            Assert.That(held, Is.EqualTo(Direction.North), "дальше — по зажатой W");
        }

        [Test]
        public void OppositeKeys_Stop()
        {
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = Wait;
            _intent.TryTakeStep(out _, out _);

            _clock.NowMs = 100;
            _intent.Press(MoveKeys.Down);

            _clock.NowMs = 250;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "W и S гасят друг друга");

            _intent.Release(MoveKeys.Up);
            _clock.NowMs = 450;
            Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True, "осталась S");
            Assert.That(direction, Is.EqualTo(Direction.South));
        }

        [Test]
        public void RunFlag_TakenAtStepTime()
        {
            _intent.Press(MoveKeys.Left);
            _intent.ToggleRun();
            _clock.NowMs = Wait;
            _intent.TryTakeStep(out _, out bool walk);
            Assert.That(walk, Is.False);

            _intent.SetShift(true);
            _clock.NowMs = 300;
            _intent.TryTakeStep(out _, out bool run);
            Assert.That(run, Is.True);
        }

        [Test]
        public void Blocked_ReleasesKeysAndShift_IgnoresInput_KeepsRunToggle()
        {
            _intent.ToggleRun();
            _intent.SetShift(true);
            _intent.Press(MoveKeys.Up);

            _intent.SetBlocked(true);
            _clock.NowMs = Wait;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "без фокуса — стоим");
            Assert.That(_intent.IsRunning, Is.False, "Shift отпущен, режим «шаг» от ~ остался");

            _intent.Press(MoveKeys.Right);
            _intent.SetShift(true);
            _intent.ToggleRun();
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "в текстовом поле W/A/S/D молчат");
            Assert.That(_intent.IsRunning, Is.False, "в текстовом поле ~ и Shift молчат");

            _intent.SetBlocked(false);
            _clock.NowMs = 100;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False, "клавиши, зажатые до блокировки, не считаются");

            _intent.Press(MoveKeys.Right);
            _clock.NowMs = 100 + Wait;
            Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True, "новое нажатие работает");
            Assert.That(direction, Is.EqualTo(Direction.East));
        }

        [Test]
        public void Blocked_ForgetsRememberedTap()
        {
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = 10;
            _intent.Release(MoveKeys.Up);

            _clock.NowMs = 20;
            _intent.SetBlocked(true);
            _clock.NowMs = 30;
            _intent.SetBlocked(false);

            _clock.NowMs = 100;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False);
        }

        [Test]
        public void RepeatedPressOfHeldKey_ChangesNothing()
        {
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = 40;
            _intent.Press(MoveKeys.Up);

            _clock.NowMs = Wait;
            Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True, "ожидание не началось заново");
            Assert.That(direction, Is.EqualTo(Direction.North));
        }

        [Test]
        public void AfterRest_NextPressWaitsAgain()
        {
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = Wait;
            _intent.TryTakeStep(out _, out _);
            _intent.Release(MoveKeys.Up);
            _clock.NowMs = 300;
            _intent.TryTakeStep(out _, out _);

            _clock.NowMs = 1000;
            _intent.Press(MoveKeys.Right);
            _clock.NowMs = 1000 + Wait - 1;
            Assert.That(_intent.TryTakeStep(out _, out _), Is.False);
            _clock.NowMs = 1000 + Wait;
            Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True);
            Assert.That(direction, Is.EqualTo(Direction.East));
        }

        [Test]
        public void Layout_ChoosesTable()
        {
            _intent.Layout = CameraLayout.Isometric;
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = Wait;

            Assert.That(_intent.TryTakeStep(out var direction, out _), Is.True);
            Assert.That(direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        public void TryTakeStep_DoesNotAllocate()
        {
            _intent.Press(MoveKeys.Up);
            _clock.NowMs = Wait;
            _intent.TryTakeStep(out _, out _);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                _clock.NowMs += 200;
                _intent.Press(MoveKeys.Right);
                _intent.TryTakeStep(out _, out _);
                _intent.Release(MoveKeys.Right);
                _intent.TryTakeStep(out _, out _);
            }

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
