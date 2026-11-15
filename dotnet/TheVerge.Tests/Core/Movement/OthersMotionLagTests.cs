using System;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using static TheVerge.Tests.Core.Movement.OthersScene;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Чужие мобы под лагом (PLAN Стадия 3, п. 8 и критерий «Лаг, чужие»; ADR 0020 пп. 4–5): пачка шагов догоняется ускоренно,
    /// но не быстрее ClientLimits.StepCatchUpMaxSpeedup; всплеск лага до 500 мс — без перескоков; картинка, отставшая
    /// больше ClientLimits.StepBacklogSnapMs, встаёт на последнюю клетку сервера. Новой памяти на шаг нет (AGENTS.md §3.1).
    /// </summary>
    public sealed class OthersMotionLagTests
    {
        private OthersScene _s = null!;

        [SetUp]
        public void SetUp()
        {
            _s = new OthersScene();
            _s.Appear(Orc, 200, 200);
        }

        [TearDown]
        public void TearDown() => _s.Dispose();

        [Test]
        public void Burst_ThreeSteps_CatchUpTwiceAsFast_LastAtNormalSpeed()
        {
            _s.Tick(0);
            _s.At(1000);
            for (short x = 201; x <= 203; x++)
            {
                _s.Move(Orc, x, 200, Dir(Direction.East, true));
            }

            _s.Tick(1000);
            _s.Tick(1100);
            _s.Tick(1200);

            Assert.That(_s.Started, Has.Count.EqualTo(3));
            AssertStep(_s.Started[0], 1000, 100, 3);
            AssertStep(_s.Started[1], 1100, 100, 2);
            AssertStep(_s.Started[2], 1200, 200, 1);
            Assert.That(_s.Started[0].IsCatchUp, Is.True);
            Assert.That(_s.Motion.CatchUpCount, Is.EqualTo(2));
            Assert.That(_s.Motion.SnapCount, Is.EqualTo(0));
        }

        [Test]
        public void CatchUp_NotFasterThanLimit()
        {
            Assert.That(ClientLimits.StepCatchUpMaxSpeedup, Is.EqualTo(2));
            _s.Mount(Orc);
            _s.Tick(0);
            _s.At(1000);
            for (short x = 201; x <= 205; x++)
            {
                _s.Move(Orc, x, 200, Dir(Direction.East, true));
            }

            _s.Tick(5000);

            Assert.That(_s.Started, Has.Count.EqualTo(5));
            foreach (var step in _s.Started)
            {
                Assert.That(step.DurationMs, Is.GreaterThanOrEqualTo(50), "бег верхом 100 мс — не быстрее чем вдвое");
            }

            Assert.That(_s.Started[1].StartMs, Is.EqualTo(_s.Started[0].EndMs), "долгий тик: шаги идут подряд по своему времени");
            Assert.That(_s.Started[4].StartMs, Is.EqualTo(_s.Started[3].EndMs));
        }

        // Моб шёл ровно, затем пакеты задержались на 500 мс и пришли разом: шаги, отправленные сервером за эти 500 мс.
        [TestCase(false, false, 2)]
        [TestCase(false, true, 3)]
        [TestCase(true, false, 3)]
        [TestCase(true, true, 5)]
        public void LagSpike500Ms_NoSnap_AllShown_FasterThanNormal(bool mounted, bool running, int bunched)
        {
            if (mounted)
            {
                _s.Mount(Orc);
            }

            int delay = StepTiming.StepDelayMs(mounted, running);
            short x = 200;
            long now = 0;
            for (int i = 0; i < 4; i++, now += delay)
            {
                _s.At(now);
                _s.Move(Orc, ++x, 200, Dir(Direction.East, running));
                _s.Tick(now);
            }

            _s.At(now + 500);
            for (int i = 0; i < bunched; i++)
            {
                _s.Move(Orc, ++x, 200, Dir(Direction.East, running));
            }

            for (long t = now + 500; t <= now + 3000; t += 16)
            {
                _s.Tick(t);
            }

            Assert.That(_s.Motion.SnapCount, Is.EqualTo(0), "всплеск лага до 500 мс — без перескоков");
            Assert.That(_s.Started, Has.Count.EqualTo(4 + bunched), "все шаги показаны");
            var last = _s.Started[^1];
            Assert.That(last.To, Is.EqualTo(new Point3D(x, 200, 0)));
            Assert.That(last.EndMs - (now + 500), Is.LessThan(bunched * delay), "догоняет ускоренно");
            Assert.That(_s.Motion.CatchUpCount, Is.GreaterThan(0));
        }

        [Test]
        public void Backlog_MoreThanLimit_SnapsToLastCell_DropsWaiting()
        {
            Assert.That(ClientLimits.StepBacklogSnapMs, Is.EqualTo(1000));
            _s.Tick(0);
            _s.At(1000);
            _s.Move(Orc, 201, 200, Direction.East);
            _s.Move(Orc, 202, 200, Direction.East);
            Assert.That(_s.Motion.SnapCount, Is.EqualTo(0), "800 мс отставания — догоняет");
            _s.Move(Orc, 203, 200, Direction.East);
            _s.Tick(1000);

            Assert.That(_s.Motion.SnapCount, Is.EqualTo(1));
            Assert.That(_s.Snaps[^1].Reason, Is.EqualTo(SnapReason.Backlog));
            Assert.That(_s.Snaps[^1].Cell, Is.EqualTo(new Point3D(203, 200, 0)));
            Assert.That(_s.Snaps[^1].DroppedSteps, Is.EqualTo(3));
            Assert.That(_s.Started, Is.Empty);
            Assert.That(_s.Step(Orc).To, Is.EqualTo(new Point3D(203, 200, 0)));
            Assert.That(_s.Motion.WaitingSteps(Orc), Is.EqualTo(0));
        }

        // Шаг пешком (400 мс) показывается с 0; ещё два приходят в момент arrival: отставание — остаток текущего и 2 × 400.
        [TestCase(300, false)]
        [TestCase(100, true)]
        public void Backlog_CountsRestOfCurrentStep(long arrival, bool snaps)
        {
            _s.Tick(0);
            _s.Move(Orc, 201, 200, Direction.East);
            _s.Tick(0);
            _s.At(arrival);
            _s.Move(Orc, 202, 200, Direction.East);
            _s.Move(Orc, 203, 200, Direction.East);

            Assert.That(_s.Motion.SnapCount, Is.EqualTo(snaps ? 1 : 0));
            if (snaps)
            {
                Assert.That(_s.Snaps[^1].DroppedSteps, Is.EqualTo(2));
            }
        }

        [Test]
        public void SteadySteps_TenMobiles_NoNewMemory()
        {
            var mobiles = new Serial[10];
            for (int i = 0; i < mobiles.Length; i++)
            {
                mobiles[i] = new Serial((uint)(0x300 + i));
                _s.Appear(mobiles[i], 200, (short)(200 + i));
            }

            _s.StopRecording();
            long now = 0;
            Walk(mobiles, ref now, 20);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Walk(mobiles, ref now, 1000);

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private void Walk(Serial[] mobiles, ref long now, int steps)
        {
            for (int n = 0; n < steps; n++, now += 200)
            {
                _s.At(now);
                for (int i = 0; i < mobiles.Length; i++)
                {
                    _s.Move(mobiles[i], (short)(n % 2 == 0 ? 201 : 200), (short)(200 + i), Dir(Direction.East, true));
                }

                _s.Tick(now);
            }
        }

        private static void AssertStep(ShownStep step, long start, int duration, int behind)
        {
            Assert.That(step.StartMs, Is.EqualTo(start));
            Assert.That(step.DurationMs, Is.EqualTo(duration));
            Assert.That(step.Behind, Is.EqualTo(behind));
        }
    }
}
