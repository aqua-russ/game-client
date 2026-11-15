using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Номера шагов и очередь шагов, ждущих ответа сервера (PLAN Стадия 3, пп. 5–6). Правила номеров —
    /// Projects/Server/Network/MovementThrottle.cs: первый шаг после входа или сброса счёта — 0, дальше 1–255 по кругу без 0.
    /// Сервер отвечает на шаги по порядку (подтверждение 0x22 — Mobile.Move, отказ 0x21 — RejectAndReset), поэтому ответ
    /// относится к самому старому ждущему шагу. Сбросив счёт (отказ, телепорт, смена карты, ресинхронизация —
    /// NetState.ResetMovementState), сервер отказывает в уже отправленных шагах с номером не 0, а стоявшие у него в очереди
    /// выбрасывает без ответа. Поэтому номера отменённых шагов запоминаются: отказ с таким номером — старый; все старые
    /// ответы приходят раньше ответа на первый новый шаг, после него номера забываются.
    /// </summary>
    public sealed class MovementSequenceQueue
    {
        // Отменённых шагов бывает не больше предела неподтверждённых на один сброс; запас — на второй сброс подряд, пока
        // старые ответы ещё в пути. Переполнение вытесняет самые давние номера: их ответы пришли первыми.
        private const int CancelledCapacity = 2 * ServerLimits.MaxUnacknowledgedSteps;

        private readonly Queue<PendingStep> _pendingSteps = new Queue<PendingStep>(ServerLimits.MaxUnacknowledgedSteps);
        private readonly byte[] _cancelled = new byte[CancelledCapacity];
        private int _cancelledCount;
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
        /// Подтверждение 0x22: снимает самый старый ждущий шаг, если номер совпал. Иначе — false, очередь цела. Номер
        /// отменённого шага — тоже false: сервер мог выполнить отменённый шаг 0 (после сброса номер 0 он принимает), и чей
        /// это ответ, неизвестно.
        /// </summary>
        public bool ConfirmStep(byte sequence, out PendingStep confirmedStep)
        {
            if (_pendingSteps.Count == 0 || _pendingSteps.Peek().Sequence != sequence || IsCancelled(sequence))
            {
                confirmedStep = default;
                return false;
            }

            confirmedStep = _pendingSteps.Dequeue();
            _cancelledCount = 0;
            return true;
        }

        /// <summary>
        /// Отказ 0x21. true — отказ настоящий: в самом старом ждущем шаге (или в шаге, которого клиент не ждал) — остальные
        /// ждущие шаги отменены, следующий номер 0. false — отказ в отменённом шаге: старый, ничего не меняется.
        /// </summary>
        public bool Reject(byte sequence)
        {
            bool current = _pendingSteps.Count > 0 && _pendingSteps.Peek().Sequence == sequence;
            if (!current && IsCancelled(sequence))
            {
                return false;
            }

            if (current)
            {
                // Ответ на нынешний шаг: старые ответы уже пришли.
                _cancelledCount = 0;
                _pendingSteps.Dequeue();
            }

            CancelPending();
            return true;
        }

        /// <summary>Сервер сам сбросил счёт (0x20, смена карты, ресинхронизация): ждущие шаги отменены, следующий номер 0.</summary>
        public void CancelPending()
        {
            while (_pendingSteps.Count > 0)
            {
                AddCancelled(_pendingSteps.Dequeue().Sequence);
            }

            _nextSequence = ServerLimits.FirstMoveSequence;
        }

        /// <summary>Новый вход в мир: прежних шагов нет, следующий номер 0.</summary>
        public void Reset()
        {
            _pendingSteps.Clear();
            _cancelledCount = 0;
            _nextSequence = ServerLimits.FirstMoveSequence;
        }

        private bool IsCancelled(byte sequence)
        {
            for (int i = 0; i < _cancelledCount; i++)
            {
                if (_cancelled[i] == sequence)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddCancelled(byte sequence)
        {
            if (_cancelledCount == CancelledCapacity)
            {
                System.Array.Copy(_cancelled, 1, _cancelled, 0, CancelledCapacity - 1);
                _cancelledCount--;
            }

            _cancelled[_cancelledCount++] = sequence;
        }
    }
}
