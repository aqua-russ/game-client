using TheVerge.Core.Movement;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// movedump — сверка физики движения с сервером: сказать [DumpMove, дождаться файла
    /// &lt;папка&gt;/move-&lt;серийник&gt;.json (<see cref="ServerDumpFile"/>) и сверить с <see cref="MovementCheck"/>
    /// через <see cref="MoveDumpComparison"/>.
    /// </summary>
    internal sealed class MoveDumpStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly BotWorldData? _data;
        private readonly ServerDumpFile _file;

        public MoveDumpStep(BotWorld world, ILog log, IClock clock, BotWorldData? data, MoveDumpRequest request)
        {
            _world = world;
            _log = log;
            _data = data;
            _file = new ServerDumpFile(world, clock, request.Command, request.Directory, "move");
            Failure = data == null ? "нужна папка файлов данных: --data <папка>." : string.Empty;
        }

        public int TimeoutMs => BotScript.LargeDumpTimeoutMs;

        public string TimeoutProblem => _file.TimeoutProblem("дамп движения");

        public string Failure { get; private set; }

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (_data == null || player == null || !_file.TryGetFreshPath(out var path))
            {
                return false;
            }

            using (var server = _file.TryParseJson(path))
            {
                if (server == null)
                {
                    return false;
                }

                var checker = new MovementCheck(_data.Map, _data.Tiles, _data.Multis, _world.Entities);
                var report = MoveDumpComparison.Compare(server.RootElement, checker, player.Serial.Value);
                var matched = report.WriteTo(_log, "Сверка физики движения с сервером: совпадает.", "физика движения расходится с сервером", path, out var failure);
                Failure = failure;
                return matched;
            }
        }

        public void Finish()
        {
        }
    }
}
