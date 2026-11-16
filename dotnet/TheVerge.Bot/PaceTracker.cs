using System;
using System.Globalization;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// Замер фактического темпа отрезка или прогулки против расписания сервера (PLAN Стадия 3, п. 12): сумма серверных
    /// длительностей шагов (<see cref="StepTiming.StepDelayMs"/>) против реального времени от первого шага до последнего.
    /// </summary>
    internal sealed class PaceTracker
    {
        private readonly IClock _clock;
        private PlayerMotion? _motion;
        private long _firstMs;
        private long _lastMs;
        private int _lastDelayMs;

        public PaceTracker(IClock clock)
        {
            _clock = clock;
        }

        public int Steps { get; private set; }

        public long ExpectedMs { get; private set; }

        public long ActualMs => Steps < 2 ? 0 : _lastMs - _firstMs;

        public bool HasTempo => Steps >= 2 && ActualMs > 0 && ExpectedMs > 0;

        public void Start(PlayerMotion motion)
        {
            Stop();
            _motion = motion;
            _motion.Moved += OnMoved;
        }

        public void Stop()
        {
            if (_motion != null)
            {
                _motion.Moved -= OnMoved;
                _motion = null;
            }
        }

        public string Describe()
        {
            long actual = ActualMs;
            long actualRate = (Steps - 1) * 100000L / actual;
            long expectedRate = (Steps - 1) * 100000L / ExpectedMs;
            long diffTenths = (actual - ExpectedMs) * 1000L / ExpectedMs;
            return "Темп: фактическое время " + actual + " мс, сервер " + ExpectedMs + " мс (" +
                   FormatPercent(diffTenths) + "; " + FormatRate(actualRate) + " против " + FormatRate(expectedRate) + " шага/с).";
        }

        private void OnMoved(StepMove move)
        {
            long now = _clock.NowMs;
            int delay = StepTiming.StepDelayMs(move.IsMounted, move.IsRunning);
            if (Steps == 0)
            {
                _firstMs = now;
            }
            else
            {
                ExpectedMs += _lastDelayMs;
            }

            _lastMs = now;
            _lastDelayMs = delay;
            Steps++;
        }

        private static string FormatRate(long per100Sec) =>
            (per100Sec / 100).ToString(CultureInfo.InvariantCulture) + "," + (per100Sec % 100).ToString("D2", CultureInfo.InvariantCulture);

        private static string FormatPercent(long tenths)
        {
            string sign = tenths > 0 ? "+" : tenths < 0 ? "-" : string.Empty;
            long abs = Math.Abs(tenths);
            return sign + (abs / 10).ToString(CultureInfo.InvariantCulture) + "," + (abs % 10).ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}