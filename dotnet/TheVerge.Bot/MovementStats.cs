using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// Общая статистика движения своего персонажа за сеанс бота (PLAN Стадия 3, п. 12): шаги, повороты, отказы 0x21,
    /// ресинхронизации и задержка подтверждения каждого запроса (0x02 → 0x22: мин, среднее, макс по всему сеансу).
    /// </summary>
    public sealed class MovementStats : IDisposable
    {
        private readonly PlayerMotion _motion;
        private readonly MovementReplies _replies;
        private readonly IClock _clock;
        private readonly long[] _sentAt = new long[256];
        private long _ackSum;
        private bool _disposed;

        public MovementStats(PlayerMotion motion, MovementReplies replies, IClock clock)
        {
            _motion = motion;
            _replies = replies;
            _clock = clock;
            _motion.Requested += OnRequested;
            _motion.Confirmed += OnConfirmed;
            _motion.Moved += OnMoved;
        }

        public int MovesCount { get; private set; }

        public int RequestsCount { get; private set; }

        public int TurnsCount => Math.Max(0, RequestsCount - MovesCount);

        public int AckCount { get; private set; }

        public int MinAckMs { get; private set; }

        public int MaxAckMs { get; private set; }

        public int AvgAckMs => AckCount == 0 ? 0 : (int)((_ackSum + AckCount / 2) / AckCount);

        public string Describe()
        {
            if (RequestsCount == 0 && _replies.RejectCount == 0 && _replies.ResyncCount == 0)
            {
                return string.Empty;
            }

            var head = "Движение: шагов " + MovesCount + ", поворотов " + TurnsCount + ", отказов сервера " +
                       _replies.RejectCount + ", ресинхронизаций " + _replies.ResyncCount;
            if (AckCount == 0)
            {
                return head + ".";
            }

            return head + "; подтверждение шага: мин " + MinAckMs + " мс, среднее " + AvgAckMs + " мс, макс " +
                   MaxAckMs + " мс (измерений " + AckCount + ").";
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _motion.Requested -= OnRequested;
            _motion.Confirmed -= OnConfirmed;
            _motion.Moved -= OnMoved;
        }

        private void OnRequested(byte sequence)
        {
            _sentAt[sequence] = _clock.NowMs;
            RequestsCount++;
        }

        private void OnConfirmed(byte sequence)
        {
            int latency = Math.Max(0, (int)(_clock.NowMs - _sentAt[sequence]));
            MinAckMs = AckCount == 0 ? latency : Math.Min(MinAckMs, latency);
            MaxAckMs = Math.Max(MaxAckMs, latency);
            _ackSum += latency;
            AckCount++;
        }

        private void OnMoved(StepMove move) => MovesCount++;
    }
}