using System;
using System.IO;
using System.Text.Json;
using TheVerge.Core.Chat;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// dump:&lt;папка&gt; — сверка мира бота с сервером (PLAN Стадия 2, п. 11): сказать [DumpVisible (команда GM,
    /// Custom/TheVerge/Parity сервера), подождать <see cref="BotScript.DumpSettleMs"/>, дождаться свежего файла
    /// &lt;папка&gt;/visible-&lt;серийник&gt;.json и сравнить со своим снимком. Свой снимок пишется рядом (.bot.json) — для разбора.
    /// </summary>
    internal sealed class DumpStep : IBotStep
    {
        private const string Command = "[DumpVisible";

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly string _directory;
        private bool _said;
        private long _saidAtMs;
        private DateTime _saidAtUtc;

        public DumpStep(BotWorld world, ILog log, IClock clock, string directory)
        {
            _world = world;
            _log = log;
            _clock = clock;
            _directory = directory;
        }

        public string TimeoutProblem => "сервер не записал дамп в " + _directory + " (нужен аккаунт GM и сервер с командой [DumpVisible).";

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            if (!_said)
            {
                // Чуть раньше «сейчас»: время файла у файловой системы бывает округлено.
                _saidAtUtc = DateTime.UtcNow.AddMilliseconds(-500);
                var result = _world.Speech.Say(Command);
                _said = result == SpeechResult.Sent || result == SpeechResult.Queued;
                _saidAtMs = _clock.NowMs;
                return false;
            }

            var player = _world.Entities.Player;
            if (_clock.NowMs - _saidAtMs < BotScript.DumpSettleMs || player == null)
            {
                return false;
            }

            var path = Path.Combine(_directory, "visible-" + player.Serial.Value.ToString("X8") + ".json");
            var server = ReadFresh(path);
            return server != null && Compare(server, path);
        }

        public void Finish()
        {
        }

        private WorldSnapshot? ReadFresh(string path)
        {
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < _saidAtUtc)
            {
                return null;
            }

            try
            {
                return WorldSnapshot.Read(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return null; // сервер ещё пишет файл — прочитаем в следующем тике
            }
            catch (IOException)
            {
                return null;
            }
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
