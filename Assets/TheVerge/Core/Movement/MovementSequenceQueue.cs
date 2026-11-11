using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Номера шагов и очередь шагов, ждущих ответа сервера (PLAN Стадия 3, пп. 5–6). Правила номеров —
    /// Projects/Server/Network/MovementThrottle.cs: первый шаг после входа или отказа — 0, дальше 1–255 по кругу
    /// без 0; отказ 0x21 сбрасывает очередь и номер. Сервер выполняет шаги по очереди и подтверждает (0x22) каждый
    /// в том же порядке (Projects/Server/Mobiles/Mobile.cs, Move), поэтому подтверждение относится к самому старому
    /// ждущему шагу, а другой номер — рассинхрон: очередь не трогаем, решение (ресинхронизация) — за вызывающим.
    /// </summary>
    public sealed class MovementSequenceQueue
    {
        private readonly Queue<PendingStep> _pendingSteps = new Queue<PendingStep>(ServerLimits.MaxUnacknowledgedSteps);
        private byte _nextSequence = ServerLimits.FirstMoveSequence;

        public int PendingCount => _pendingSteps.Count;
        public bool CanRequestStep => _pendingSteps.Count < ServerLimits.MaxUnacknowledgedSteps;

        /// <summary>
        /// Даёт следующий номер и запоминает шаг как неподтверждённый. Если без ответа уже висит предельное число
        /// шагов (ServerLimits.MaxUnacknowledgedSteps), шаг не ставится и номер не расходуется.
        /// </summary>
        public bool TryEnqueueStep(Direction direction, Point3D targetPosition, out byte sequence)
        {
            if (!CanRequestStep)
            {
                sequence = 0;
                return false;
            }

            sequence = _nextSequence;
            _nextSequence = sequence == ServerLimits.MaxMoveSequence
                ? ServerLimits.WrappedMoveSequence
                : (byte)(sequence + 1);
            _pendingSteps.Enqueue(new PendingStep(sequence, direction, targetPosition));
            return true;
        }

        /// <summary>
        /// Подтверждение 0x22: снимает самый старый ждущий шаг, если номер совпал. Иначе — false, очередь цела.
        /// </summary>
        public bool ConfirmStep(byte sequence, out PendingStep confirmedStep)
        {
            if (_pendingSteps.Count == 0 || _pendingSteps.Peek().Sequence != sequence)
            {
                confirmedStep = default;
                return false;
            }

            confirmedStep = _pendingSteps.Dequeue();
            return true;
        }

        /// <summary>
        /// Отказ сервера 0x21 или ресинхронизация: очередь пуста, следующий шаг снова с номером 0.
        /// </summary>
        public void Reset()
        {
            _pendingSteps.Clear();
            _nextSequence = ServerLimits.FirstMoveSequence;
        }
    }
}
