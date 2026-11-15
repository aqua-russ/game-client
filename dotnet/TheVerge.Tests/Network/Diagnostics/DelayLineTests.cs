using NUnit.Framework;
using TheVerge.Core.Diagnostics;
using TheVerge.Network.Diagnostics;
using TheVerge.Tests.Core.Diagnostics;

namespace TheVerge.Tests.Network.Diagnostics
{
    /// <summary>
    /// Очередь задержки одного направления (симуляция пинга, PLAN Стадия 3, п. 9): элемент выходит не раньше своего срока и
    /// не раньше всех, кто пришёл до него. Пакеты в TCP не обгоняют друг друга: разброс задержки даёт пачки, а не
    /// перестановку (поэтому протокол не замечает симуляции).
    /// </summary>
    public sealed class DelayLineTests
    {
        [Test]
        public void NoLag_OutRightAway()
        {
            var line = new DelayLine<int>(new StepRandom(0));

            line.Push(7, 1000, LagSettings.None);

            Assert.That(line.TryPop(1000, out var item), Is.True);
            Assert.That(item, Is.EqualTo(7));
            Assert.That(line.Count, Is.EqualTo(0));
        }

        [Test]
        public void Delay100_NotBefore1100()
        {
            var line = new DelayLine<int>(new StepRandom(0));

            line.Push(7, 1000, new LagSettings(100, 0));

            Assert.That(line.TryPop(1099, out _), Is.False);
            Assert.That(line.Count, Is.EqualTo(1));
            Assert.That(line.TryPop(1100, out var item), Is.True);
            Assert.That(item, Is.EqualTo(7));
        }

        [Test]
        public void Jitter_LaterPacketNeverOvertakes()
        {
            var line = new DelayLine<int>(new StepRandom(30, -30));
            var lag = new LagSettings(100, 30);

            line.Push(1, 1000, lag);
            line.Push(2, 1010, lag);

            Assert.That(line.TryPop(1129, out _), Is.False, "второй (срок 1080) ждёт первого (срок 1130)");
            Assert.That(line.TryPop(1130, out var first), Is.True);
            Assert.That(line.TryPop(1130, out var second), Is.True);
            Assert.That((first, second), Is.EqualTo((1, 2)));
        }

        [Test]
        public void LagTurnedOff_QueuedLeaveFirst_ThenNewOnes()
        {
            var line = new DelayLine<int>(new StepRandom(0));
            line.Push(1, 1000, new LagSettings(200, 0));

            line.Push(2, 1050, LagSettings.None);

            Assert.That(line.TryPop(1050, out _), Is.False, "новый пакет без задержки ждёт старый");
            Assert.That(line.TryPop(1200, out var first), Is.True);
            Assert.That(line.TryPop(1200, out var second), Is.True);
            Assert.That((first, second), Is.EqualTo((1, 2)));
        }

        [Test]
        public void TryPopAny_EmptiesRegardlessOfTime()
        {
            var line = new DelayLine<int>(new StepRandom(0));
            line.Push(1, 1000, new LagSettings(500, 0));
            line.Push(2, 1000, new LagSettings(500, 0));

            Assert.That(line.TryPopAny(out var first), Is.True);
            Assert.That(line.TryPopAny(out var second), Is.True);
            Assert.That(line.TryPopAny(out _), Is.False);
            Assert.That((first, second), Is.EqualTo((1, 2)));
        }

        [Test]
        public void SteadyTraffic_NoNewMemory()
        {
            var line = new DelayLine<int>(new System.Random(1));
            var lag = new LagSettings(100, 30);
            for (int i = 0; i < 100; i++)
            {
                line.Push(i, 1000 + i, lag);
            }

            while (line.TryPopAny(out _))
            {
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                line.Push(i, 5000 + i, lag);
                line.TryPop(5000 + i, out _);
            }

            while (line.TryPopAny(out _))
            {
            }

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
