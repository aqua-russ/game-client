using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Время шага (PLAN Стадия 3, п. 7; ADR 0020 пп. 4–6). Задержка между шагами у сервера — таблица «пешком или верхом ×
    /// шаг или бег» (docs/TARGET.md §3: movement.delay.walkFoot / runFoot / walkMount / runMount = 400 / 200 / 200 / 100 мс;
    /// Projects/UOContent/Mobiles/PlayerMobile.cs, ComputeMovementSpeed). Картинка показывает шаг столько же; отставшая
    /// картинка догоняет быстрее, но не больше чем в ClientLimits.StepCatchUpMaxSpeedup раз; разрыв больше
    /// ClientLimits.StepSnapGapCells клеток — перескок, а не езда.
    /// </summary>
    public sealed class StepTimingTests
    {
        [TestCase(false, false, 400)]
        [TestCase(false, true, 200)]
        [TestCase(true, false, 200)]
        [TestCase(true, true, 100)]
        public void StepDelay_FootOrMount_WalkOrRun(bool mounted, bool running, int expectedMs)
        {
            Assert.That(StepTiming.StepDelayMs(mounted, running), Is.EqualTo(expectedMs));
            Assert.That(StepTiming.DisplayMs(mounted, running, 1), Is.EqualTo(expectedMs), "показ шага — за то же время");
        }

        [Test]
        public void CatchUp_FasterWhenBehind_NotFasterThanLimit()
        {
            Assert.That(ClientLimits.StepCatchUpMaxSpeedup, Is.EqualTo(2));

            Assert.That(StepTiming.DisplayMs(false, true, 2), Is.EqualTo(100), "отстала на 2 клетки — вдвое быстрее");
            Assert.That(StepTiming.DisplayMs(false, true, 5), Is.EqualTo(100), "не быстрее предела");
            Assert.That(StepTiming.DisplayMs(true, true, 3), Is.EqualTo(50));
            Assert.That(StepTiming.DisplayMs(false, false, 0), Is.EqualTo(400), "не отстала — обычное время");
        }

        [Test]
        public void Snap_OnlyWhenGapMoreThanTwoCells()
        {
            Assert.That(ClientLimits.StepSnapGapCells, Is.EqualTo(2));
            var shown = new Point3D(100, 100, 0);

            Assert.That(StepTiming.ShouldSnap(shown, shown), Is.False);
            Assert.That(StepTiming.ShouldSnap(shown, new Point3D(102, 101, 0)), Is.False, "2 клетки — едет");
            Assert.That(StepTiming.ShouldSnap(shown, new Point3D(98, 98, 10)), Is.False, "по диагонали 2 клетки — едет");
            Assert.That(StepTiming.ShouldSnap(shown, new Point3D(103, 100, 0)), Is.True, "3 клетки — перескок");
            Assert.That(StepTiming.ShouldSnap(shown, new Point3D(97, 103, 0)), Is.True);
        }
    }
}
