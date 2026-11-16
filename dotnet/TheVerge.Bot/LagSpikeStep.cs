using System.Globalization;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>
    /// lagspike:&lt;мс&gt; (PLAN Стадия 3, п. 12): устроить разовый входящий всплеск задержки и дождаться выхода и
    /// проигрывания пришедшей пачки шагов.
    /// </summary>
    internal sealed class LagSpikeStep : IBotStep
    {
        private const int SettleMs = 1000;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly int _durationMs;
        private long _startedAt = -1;

        public LagSpikeStep(BotWorld world, ILog log, IClock clock, string argument)
        {
            _world = world;
            _log = log;
            _clock = clock;
            TryParse(argument, out _durationMs);
        }

        public int TimeoutMs => _durationMs + SettleMs + 5000;

        public string TimeoutProblem => "всплеск лага " + _durationMs + " мс не завершился.";

        public string Failure { get; private set; } = string.Empty;

        public static bool TryParse(string argument, out int durationMs) =>
            int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out durationMs) &&
            durationMs > 0 && durationMs <= ClientLimits.LagSimulationMaxDelayMs;

        public bool Poll()
        {
            if (_world.Lag == null)
            {
                Failure = "симуляция лага не подключена.";
                return false;
            }

            if (_startedAt < 0)
            {
                _world.Lag.TriggerIncomingSpike(_durationMs);
                _startedAt = _clock.NowMs;
                _log.Info("Всплеск входящего лага: " + _durationMs + " мс.");
                return false;
            }

            return _clock.NowMs - _startedAt >= _durationMs + SettleMs;
        }

        public void Finish()
        {
        }
    }
}