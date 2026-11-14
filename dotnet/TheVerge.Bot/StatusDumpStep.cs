using System;
using System.IO;
using System.Text.Json;
using TheVerge.Core.Chat;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// statusdump:&lt;папка&gt; — сверка состояния мобов с сервером (PLAN Стадия 4, п. 4): запросить статус всех мобов, которых
    /// бот видит (0x34; чужим без запроса сервер статус не шлёт), подождать <see cref="BotScript.DumpSettleMs"/>, сказать
    /// [DumpMobile (команда GM), подождать ещё и сверить свежий файл &lt;папка&gt;/mobiles-&lt;серийник&gt;.json
    /// (<see cref="StatusComparison"/>).
    /// </summary>
    internal sealed class StatusDumpStep : IBotStep
    {
        private const string Command = "[DumpMobile";

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly string _directory;
        private long _askedAtMs = -1;
        private long _saidAtMs = -1;
        private DateTime _saidAtUtc;

        public StatusDumpStep(BotWorld world, ILog log, IClock clock, string directory)
        {
            _world = world;
            _log = log;
            _clock = clock;
            _directory = directory;
            Failure = world.Status == null ? "состояние мобов у бота не подключено." : string.Empty;
        }

        public string TimeoutProblem => "сервер не записал дамп состояния в " + _directory + " (нужен аккаунт GM и сервер с командой [DumpMobile).";

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

            return _clock.NowMs - _askedAtMs >= BotScript.DumpSettleMs && SayOrCompare();
        }

        public void Finish()
        {
        }

        private bool SayOrCompare()
        {
            if (_saidAtMs < 0)
            {
                // Чуть раньше «сейчас»: время файла у файловой системы бывает округлено.
                _saidAtUtc = DateTime.UtcNow.AddMilliseconds(-500);
                var result = _world.Speech.Say(Command);
                _saidAtMs = result == SpeechResult.Sent || result == SpeechResult.Queued ? _clock.NowMs : -1;
                return false;
            }

            var player = _world.Entities.Player;
            if (_clock.NowMs - _saidAtMs < BotScript.DumpSettleMs || player == null)
            {
                return false;
            }

            var path = Path.Combine(_directory, "mobiles-" + player.Serial.Value.ToString("X8") + ".json");
            using (var server = ReadFresh(path))
            {
                return server != null && Report(StatusComparison.Compare(server.RootElement, _world.Entities), path);
            }
        }

        private JsonDocument? ReadFresh(string path)
        {
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < _saidAtUtc)
            {
                return null;
            }

            try
            {
                return JsonDocument.Parse(File.ReadAllBytes(path));
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

        private bool Report(ParityReport report, string path)
        {
            foreach (var line in report.Summary)
            {
                _log.Info(line);
            }

            if (report.DifferenceCount == 0)
            {
                _log.Info("Сверка состояния с сервером: совпадает.");
                return true;
            }

            foreach (var line in report.Differences)
            {
                _log.Warning("Расхождение: " + line);
            }

            Failure = "состояние мобов расходится с сервером: " + report.DifferenceCount + " (первое — " + report.Differences[0] + "). Дамп: " + path + ".";
            return false;
        }
    }
}
