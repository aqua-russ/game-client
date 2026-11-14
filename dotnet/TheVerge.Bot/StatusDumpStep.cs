using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// statusdump:&lt;папка&gt; — сверка состояния мобов с сервером (PLAN Стадия 4, п. 4): запросить статус всех мобов, которых
    /// бот видит (0x34; чужим без запроса сервер статус не шлёт), подождать <see cref="BotScript.DumpSettleMs"/>, сказать
    /// [DumpMobile (команда GM) и сверить свежий файл &lt;папка&gt;/mobiles-&lt;серийник&gt;.json (<see cref="ServerDumpFile"/>,
    /// <see cref="StatusComparison"/>).
    /// </summary>
    internal sealed class StatusDumpStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly ServerDumpFile _file;
        private long _askedAtMs = -1;

        public StatusDumpStep(BotWorld world, ILog log, IClock clock, string directory)
        {
            _world = world;
            _log = log;
            _clock = clock;
            _file = new ServerDumpFile(world, clock, "[DumpMobile", directory, "mobiles");
            Failure = world.Status == null ? "состояние мобов у бота не подключено." : string.Empty;
        }

        public string TimeoutProblem => _file.TimeoutProblem("дамп состояния");

        public string Failure { get; private set; }

        public bool Poll()
        {
            if (_world.Status == null)
            {
                return false;
            }

            if (_askedAtMs < 0)
            {
                foreach (var mobile in _world.Entities.Mobiles)
                {
                    _world.Status.Commands.RequestStatus(mobile.Serial);
                }

                _askedAtMs = _clock.NowMs;
                return false;
            }

            return _clock.NowMs - _askedAtMs >= BotScript.DumpSettleMs && Compare();
        }

        public void Finish()
        {
        }

        private bool Compare()
        {
            if (!_file.TryGetFreshPath(out var path))
            {
                return false;
            }

            using (var server = _file.TryParseJson(path))
            {
                if (server == null)
                {
                    return false;
                }

                var report = StatusComparison.Compare(server.RootElement, _world.Entities);
                var matched = report.WriteTo(_log, "Сверка состояния с сервером: совпадает.", "состояние мобов расходится с сервером", path, out var failure);
                Failure = failure;
                return matched;
            }
        }
    }
}
