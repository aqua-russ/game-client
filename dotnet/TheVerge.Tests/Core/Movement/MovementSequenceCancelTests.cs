using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Отменённые шаги (PLAN Стадия 3, п. 6). Сервер отвечает на каждый шаг по порядку (Projects/Server/Network/
    /// MovementThrottle.cs), но после сброса счёта (RejectAndReset, NetState.ResetMovementState — отказ, телепорт, смена
    /// карты, ресинхронизация) шаги, которые клиент уже отправил, приходят к серверу с номером не 0 при счёте 0 — сервер
    /// отказывает в каждом (ValidateAndQueueMovement: Sequence == 0 и seq != 0), а стоявшие у него в очереди выбрасывает без
    /// ответа (_movementQueue.Clear). Поэтому клиент запоминает номера отменённых шагов: отказ с таким номером — старый,
    /// его пропускают. Все старые ответы приходят раньше ответа на первый новый шаг (порядок TCP), после него номера
    /// забываются. Подтверждение 0x22 сервер подписывает своим счётчиком (Mobile.Move: SendMovementAck(Sequence)), поэтому
    /// подтверждение с номером отменённого шага — рассинхрон: сервер выполнил шаг, которого клиент уже не ждёт.
    /// </summary>
    public sealed class MovementSequenceCancelTests
    {
        private static readonly Point3D Here = new Point3D(100, 100, 0);

        [Test]
        public void Reject_CurrentStep_CancelsTheRest_NextSequenceZero()
        {
            var queue = Queue(3);

            Assert.That(queue.Reject(0), Is.True, "отказ в самом старом шаге — настоящий");
            Assert.That(queue.PendingCount, Is.EqualTo(0));
            Assert.That(queue.Reject(1), Is.False, "отменённый шаг — старый отказ");
            Assert.That(queue.Reject(2), Is.False);

            queue.TryEnqueueStep(Direction.North, Here, out byte next);
            Assert.That(next, Is.EqualTo(ServerLimits.FirstMoveSequence));
        }

        [Test]
        public void CancelPending_ByServerReset_LaterRejectsAreOld()
        {
            var queue = Queue(2);

            queue.CancelPending();

            Assert.That(queue.PendingCount, Is.EqualTo(0));
            Assert.That(queue.Reject(0), Is.False);
            Assert.That(queue.Reject(1), Is.False);
            queue.TryEnqueueStep(Direction.North, Here, out byte next);
            Assert.That(next, Is.EqualTo(0));
        }

        [Test]
        public void OldRejectWhileNewStepsWait_DoesNotTouchNewSteps()
        {
            var queue = Queue(3);
            queue.Reject(0);
            queue.TryEnqueueStep(Direction.North, Here, out _);
            queue.TryEnqueueStep(Direction.North, Here, out byte second);
            Assert.That(second, Is.EqualTo(1), "новый счёт совпадает по номеру с отменённым шагом 1");

            Assert.That(queue.Reject(1), Is.False, "сначала приходят старые ответы: самый старый новый шаг — 0, а не 1");
            Assert.That(queue.Reject(2), Is.False);
            Assert.That(queue.PendingCount, Is.EqualTo(2));
            Assert.That(queue.ConfirmStep(0, out _), Is.True);
            Assert.That(queue.ConfirmStep(1, out _), Is.True);
        }

        [Test]
        public void FirstAnswerToNewStep_ForgetsCancelled()
        {
            var queue = Queue(3);
            queue.Reject(0);
            queue.TryEnqueueStep(Direction.North, Here, out _);
            queue.ConfirmStep(0, out _);
            queue.TryEnqueueStep(Direction.North, Here, out byte one);
            queue.TryEnqueueStep(Direction.North, Here, out byte two);

            Assert.That(queue.Reject(one), Is.True, "старых ответов уже не будет: отказ относится к новому шагу");
            Assert.That(queue.Reject(two), Is.False, "а следующий за ним — снова отменённый");
        }

        [Test]
        public void ConfirmWithCancelledNumber_IsDesync()
        {
            var queue = new MovementSequenceQueue();
            queue.TryEnqueueStep(Direction.North, Here, out byte first);
            queue.CancelPending();
            queue.TryEnqueueStep(Direction.North, Here, out byte fresh);
            Assert.That(fresh, Is.EqualTo(first), "оба шага — с номером 0");

            Assert.That(queue.ConfirmStep(0, out _), Is.False,
                "сервер мог выполнить отменённый шаг 0 (после сброса счёта номер 0 он принимает): неизвестно, чей это ответ");
            Assert.That(queue.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectZero_WhenCancelledAndNewBothZero_IsReal()
        {
            var queue = new MovementSequenceQueue();
            queue.TryEnqueueStep(Direction.North, Here, out _);
            queue.CancelPending();
            queue.TryEnqueueStep(Direction.North, Here, out _);

            Assert.That(queue.Reject(0), Is.True, "отказ несёт клетку сервера, и сервер сбросил счёт: принять его безопасно");
            Assert.That(queue.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void UnknownReject_IsReal()
        {
            var queue = Queue(1);

            Assert.That(queue.Reject(77), Is.True, "отказ, которого клиент не ждал, — сервер всё равно сбросил счёт");
            Assert.That(queue.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void Reset_ForgetsCancelled()
        {
            var queue = Queue(3);
            queue.CancelPending();

            queue.Reset();

            Assert.That(queue.Reject(1), Is.True, "после нового входа в мир прежних шагов нет");
        }

        [Test]
        public void ManyResetsInARow_KeepNewestCancelled_NoAllocation()
        {
            var queue = new MovementSequenceQueue();
            for (int round = 0; round < 4; round++)
            {
                FillAndCancel(queue);
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int round = 0; round < 50; round++)
            {
                FillAndCancel(queue);
            }

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
            Assert.That(queue.Reject(2), Is.False, "номера последнего сброса помнятся");
        }

        private static void FillAndCancel(MovementSequenceQueue queue)
        {
            for (int i = 0; i < ServerLimits.MaxUnacknowledgedSteps; i++)
            {
                queue.TryEnqueueStep(Direction.North, Here, out _);
            }

            queue.CancelPending();
        }

        private static MovementSequenceQueue Queue(int steps)
        {
            var queue = new MovementSequenceQueue();
            for (int i = 0; i < steps; i++)
            {
                queue.TryEnqueueStep(Direction.North, Here, out _);
            }

            return queue;
        }
    }
}
