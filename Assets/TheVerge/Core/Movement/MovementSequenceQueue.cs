using System.Collections.Generic;
using TheVerge.Core.Primitives;

namespace TheVerge.Core
{
    /// <summary>
    /// Очередь порядковых номеров шагов (Sequence 0..255) протокола UO.
    /// Защищает от десинхронизации и античита (Fastwalk).
    /// </summary>
    public sealed class MovementSequenceQueue
    {
        private const int MaxPendingSteps = 5;
        private readonly Queue<PendingStep> _pendingSteps = new Queue<PendingStep>(MaxPendingSteps);
        private byte _nextSequence = 0;

        public readonly struct PendingStep
        {
            public byte Sequence { get; }
            public Direction Direction { get; }
            public Point3D TargetPosition { get; }

            public PendingStep(byte sequence, Direction direction, Point3D targetPosition)
            {
                Sequence = sequence;
                Direction = direction;
                TargetPosition = targetPosition;
            }
        }

        public int PendingCount => _pendingSteps.Count;
        public bool CanRequestStep => _pendingSteps.Count < MaxPendingSteps;

        /// <summary>
        /// Выделяет следующий номер шага (Sequence 0..255) и регистрирует неподтверждённый шаг.
        /// </summary>
        public bool TryEnqueueStep(Direction direction, Point3D targetPosition, out byte sequence)
        {
            if (!CanRequestStep)
            {
                sequence = 0;
                return false;
            }

            sequence = _nextSequence;

            // Первый шаг после входа или сброса имеет номер 0, далее 1..255 и после 255 снова 1
            if (_nextSequence == 255)
            {
                _nextSequence = 1;
            }
            else
            {
                _nextSequence++;
            }

            _pendingSteps.Enqueue(new PendingStep(sequence, direction, targetPosition));
            return true;
        }

        /// <summary>
        /// Подтверждает шаг по пришедшему от сервера номеру Sequence (пакет 0x22 Move Ack).
        /// </summary>
        public bool ConfirmStep(byte sequence, out PendingStep confirmedStep)
        {
            while (_pendingSteps.Count > 0)
            {
                var step = _pendingSteps.Dequeue();
                if (step.Sequence == sequence)
                {
                    confirmedStep = step;
                    return true;
                }
            }

            confirmedStep = default;
            return false;
        }

        /// <summary>
        /// Сбрасывает очередь при отказе сервера (пакет 0x21 Move Reject) или ресинхронизации.
        /// </summary>
        public void Reset()
        {
            _pendingSteps.Clear();
            _nextSequence = 0;
        }
    }
}
