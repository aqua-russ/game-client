using System;
using TheVerge.Core.Ports;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Задержка подтверждения шага для экрана отладки (PLAN Стадия 3, п. 9): от отправки запроса шага или поворота (0x02)
    /// до подтверждения этого запроса (0x22). Сервер отвечает на запросы по порядку, запрос узнаётся по номеру
    /// (<see cref="PlayerMotion.Requested"/>, <see cref="PlayerMotion.Confirmed"/>); отменённые откатом подтверждения не
    /// получают, а номер, занятый снова после отката, получает новое время отправки. Последняя, средняя (с округлением) и
    /// наибольшая задержка — по последним <see cref="Window"/> подтверждениям. Время — <see cref="IClock"/>; новой памяти
    /// на шаг нет.
    /// </summary>
    public sealed class StepAckLatency : IDisposable
    {
        /// <summary>Сколько последних подтверждений входит в среднюю и наибольшую задержку.</summary>
        public const int Window = 32;

        private readonly PlayerMotion _motion;
        private readonly IClock _clock;
        private readonly long[] _sentMs = new long[256];
        private readonly int[] _samples = new int[Window];
        private int _next;
        private int _filled;
        private long _sum;
        private bool _disposed;

        public StepAckLatency(PlayerMotion motion, IClock clock)
        {
            _motion = motion;
            _clock = clock;
            _motion.Requested += OnRequested;
            _motion.Confirmed += OnConfirmed;
        }

        /// <summary>Сколько подтверждений измерено с начала.</summary>
        public int Count { get; private set; }

        /// <summary>Задержка последнего подтверждения, мс; 0 — подтверждений не было.</summary>
        public int LastMs { get; private set; }

        /// <summary>Средняя задержка по окну, мс (с округлением); 0 — подтверждений не было.</summary>
        public int AverageMs => _filled == 0 ? 0 : (int)((_sum + _filled / 2) / _filled);

        /// <summary>Наибольшая задержка по окну, мс; 0 — подтверждений не было.</summary>
        public int MaxMs
        {
            get
            {
                int max = 0;
                for (int i = 0; i < _filled; i++)
                {
                    max = Math.Max(max, _samples[i]);
                }

                return max;
            }
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
        }

        private void OnRequested(byte sequence) => _sentMs[sequence] = _clock.NowMs;

        private void OnConfirmed(byte sequence)
        {
            int latency = (int)(_clock.NowMs - _sentMs[sequence]);
            if (_filled == Window)
            {
                _sum -= _samples[_next];
            }
            else
            {
                _filled++;
            }

            _samples[_next] = latency;
            _sum += latency;
            _next = (_next + 1) % Window;
            LastMs = latency;
            Count++;
        }
    }
}
