using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Номера шагов и очередь неподтверждённых шагов (PLAN Стадия 3, пп. 5–6). Правила сервера:
    /// Projects/Server/Network/MovementThrottle.cs — первый шаг после входа или отказа должен иметь номер 0
    /// (иначе отказ), дальше номер растёт, после 255 идёт 1; отказ (RejectAndReset) сбрасывает очередь и номер в 0.
    /// Сервер выполняет шаги строго по очереди и на каждый шлёт подтверждение 0x22 (Mobiles/Mobile.cs, Move),
    /// поэтому подтверждение всегда относится к самому старому ждущему шагу; другое — рассинхрон.
    /// Сколько шагов держать без ответа — настройка клиента (docs/TARGET.md §3, ServerLimits).
    /// </summary>
    public sealed class MovementSequenceQueueTests
    {
        private static readonly Point3D Here = new Point3D(100, 100, 0);

        [Test]
        public void FirstStep_HasSequenceZero()
        {
            var queue = new MovementSequenceQueue();

            queue.TryEnqueueStep(Direction.North, Here, out byte sequence);

            Assert.That(sequence, Is.EqualTo(0));
        }

        [Test]
        public void Sequence_GoesUpTo255ThenWrapsToOne()
        {
            var queue = new MovementSequenceQueue();
            var sequences = new List<int>();
            for (int i = 0; i < 300; i++)
            {
                queue.TryEnqueueStep(Direction.North, Here, out byte sequence);
                queue.ConfirmStep(sequence, out _);
                sequences.Add(sequence);
            }

            var expected = new List<int>();
            for (int i = 0; i < 300; i++)
            {
                expected.Add(i <= 255 ? i : i - 255);
            }

            Assert.That(sequences, Is.EqualTo(expected));
        }

        [Test]
        public void PendingSteps_LimitedByServerLimits()
        {
            var queue = new MovementSequenceQueue();
            for (int i = 0; i < ServerLimits.MaxUnacknowledgedSteps; i++)
            {
                Assert.That(queue.TryEnqueueStep(Direction.North, Here, out _), Is.True, "шаг " + i);
            }

            bool extra = queue.TryEnqueueStep(Direction.North, Here, out _);

            Assert.That(extra, Is.False, "лишний шаг без ответа сервера не уходит");
            Assert.That(queue.CanRequestStep, Is.False);
            Assert.That(queue.PendingCount, Is.EqualTo(ServerLimits.MaxUnacknowledgedSteps));
        }

        [Test]
        public void RefusedStep_DoesNotUseSequenceNumber()
        {
            var queue = new MovementSequenceQueue();
            for (int i = 0; i < ServerLimits.MaxUnacknowledgedSteps; i++)
            {
                queue.TryEnqueueStep(Direction.North, Here, out _);
            }

            queue.TryEnqueueStep(Direction.North, Here, out _);
            queue.ConfirmStep(0, out _);
            queue.TryEnqueueStep(Direction.North, Here, out byte next);

            Assert.That(next, Is.EqualTo(ServerLimits.MaxUnacknowledgedSteps));
        }

        [Test]
        public void Confirm_OldestStep_RemovesItAndReturnsIt()
        {
            var queue = new MovementSequenceQueue();
            var target = Here.GetStepPoint(Direction.East);
            queue.TryEnqueueStep(Direction.East, target, out byte first);
            queue.TryEnqueueStep(Direction.South, target.GetStepPoint(Direction.South), out _);

            bool confirmed = queue.ConfirmStep(first, out var step);

            Assert.That(confirmed, Is.True);
            Assert.That(step.Sequence, Is.EqualTo(first));
            Assert.That(step.Direction, Is.EqualTo(Direction.East));
            Assert.That(step.TargetPosition, Is.EqualTo(target));
            Assert.That(queue.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void Reset_ClearsQueueAndNextSequenceIsZero()
        {
            var queue = new MovementSequenceQueue();
            queue.TryEnqueueStep(Direction.North, Here, out _);
            queue.TryEnqueueStep(Direction.North, Here, out _);

            queue.Reset();
            queue.TryEnqueueStep(Direction.North, Here, out byte sequence);

            Assert.That(sequence, Is.EqualTo(0));
            Assert.That(queue.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void Confirm_UnknownSequence_KeepsPendingSteps()
        {
            var queue = new MovementSequenceQueue();
            queue.TryEnqueueStep(Direction.North, Here, out _);
            queue.TryEnqueueStep(Direction.North, Here, out _);

            bool confirmed = queue.ConfirmStep(200, out _);

            Assert.That(confirmed, Is.False, "чужой номер — рассинхрон, а не подтверждение");
            Assert.That(queue.PendingCount, Is.EqualTo(2), "ждущие шаги не теряются");
        }

        [Test]
        public void Confirm_LaterStepFirst_DoesNotSkipOlderStep()
        {
            var queue = new MovementSequenceQueue();
            queue.TryEnqueueStep(Direction.North, Here, out _);
            queue.TryEnqueueStep(Direction.North, Here, out byte second);

            bool confirmed = queue.ConfirmStep(second, out _);

            Assert.That(confirmed, Is.False, "сервер подтверждает шаги только по порядку");
            Assert.That(queue.PendingCount, Is.EqualTo(2));
        }
    }
}
