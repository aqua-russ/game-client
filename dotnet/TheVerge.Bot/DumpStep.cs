using System.IO;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// dump:&lt;папка&gt; — сверка мира бота с сервером (PLAN Стадия 2, п. 11): сказать [DumpVisible (команда GM,
    /// Custom/TheVerge/Parity сервера), дождаться свежего файла &lt;папка&gt;/visible-&lt;серийник&gt;.json
    /// (<see cref="ServerDumpFile"/>) и сравнить со своим снимком. Свой снимок пишется рядом (.bot.json) — для разбора.
    /// </summary>
    internal sealed class DumpStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ServerDumpFile _file;

        public DumpStep(BotWorld world, ILog log, IClock clock, string directory)
        {
            _world = world;
            _log = log;
            _file = new ServerDumpFile(world, clock, "[DumpVisible", directory, "visible");
        }

        public string TimeoutProblem => _file.TimeoutProblem("дамп");

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            if (!_file.TryGetFreshPath(out var path))
            {
                return false;
            }

            var server = _file.TryRead(path, p => WorldSnapshot.Read(File.ReadAllText(p)));
            return server != null && Compare(server, path);
        }

        public void Finish()
        {
        }

        private bool Compare(WorldSnapshot server, string path)
        {
            var mine = WorldSnapshot.FromStore(_world.Entities);
            File.WriteAllText(path + ".bot.json", WorldSnapshot.Write(mine));
            var differences = WorldSnapshot.Compare(mine, server);
            if (differences.Count == 0)
            {
                _log.Info("Сверка с сервером: совпадает объектов " + server.Objects.Count + ".");
                return true;
            }

            foreach (var line in differences)
            {
                _log.Warning("Расхождение: " + line);
            }

            Failure = "мир бота расходится с сервером: " + differences.Count + " (первое — " + differences[0] + ") Снимки: " + path + " и .bot.json.";
            return false;
        }
    }
}
