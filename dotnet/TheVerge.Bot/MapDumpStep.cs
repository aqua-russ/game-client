using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// mapdump — сверка файлов данных (--data) с сервером (PLAN Стадия 2, п. 11б): сказать [DumpMapData (команда GM,
    /// Custom/TheVerge/Parity сервера), дождаться свежего файла &lt;папка&gt;/mapdata-&lt;серийник&gt;.json
    /// (<see cref="ServerDumpFile"/>) и сверить его с <see cref="MapDataComparison"/>.
    /// </summary>
    internal sealed class MapDumpStep : IBotStep
    {
        private readonly ILog _log;
        private readonly BotWorldData? _data;
        private readonly ServerDumpFile _file;

        public MapDumpStep(BotWorld world, ILog log, IClock clock, BotWorldData? data, MapDumpRequest request)
        {
            _log = log;
            _data = data;
            _file = new ServerDumpFile(world, clock, request.Command, request.Directory, "mapdata");
            Failure = data == null ? "нужна папка файлов данных: --data <папка>." : string.Empty;
        }

        public string TimeoutProblem => _file.TimeoutProblem("дамп карты");

        public string Failure { get; private set; }

        public bool Poll()
        {
            if (_data == null || !_file.TryGetFreshPath(out var path))
            {
                return false;
            }

            using (var server = _file.TryParseJson(path))
            {
                if (server == null)
                {
                    return false;
                }

                var report = MapDataComparison.Compare(server.RootElement, _data);
                var matched = report.WriteTo(_log, "Сверка карты с сервером: совпадает.", "данные карты расходятся с сервером", path, out var failure);
                Failure = failure;
                return matched;
            }
        }

        public void Finish()
        {
        }
    }
}
