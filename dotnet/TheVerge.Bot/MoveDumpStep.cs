using System;
using System.IO;
using System.Text.Json;
using TheVerge.Core.Chat;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// movedump — сверка физики движения с сервером: сказать [DumpMove, дождаться файла
    /// &lt;папка&gt;/move-&lt;серийник&gt;.json и сверить с <see cref="MovementCheck"/> через <see cref="MoveDumpComparison"/>.
    /// </summary>
    internal sealed class MoveDumpStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly BotWorldData? _data;
        private readonly MoveDumpRequest _request;
        private bool _said;
        private long _saidAtMs;
        private DateTime _saidAtUtc;

        public MoveDumpStep(BotWorld world, ILog log, IClock clock, BotWorldData? data, MoveDumpRequest request)
        {
            _world = world;
            _log = log;
            _clock = clock;
            _data = data;
            _request = request;
            Failure = data == null ? "нужна папка файлов данных: --data <папка>." : string.Empty;
        }

        public int TimeoutMs => BotScript.FightTimeoutMs;

        public string TimeoutProblem =>
            "сервер не записал дамп движения в " + _request.Directory + " (нужен аккаунт GM и сервер с командой [DumpMove).";

        public string Failure { get; private set; }

        public bool Poll()
        {
            if (_data == null)
            {
                return false;
            }

            if (!_said)
            {
                _saidAtUtc = DateTime.UtcNow.AddMilliseconds(-500);
                var result = _world.Speech.Say(_request.Command);
                _said = result == SpeechResult.Sent || result == SpeechResult.Queued;
                _saidAtMs = _clock.NowMs;
                return false;
            }

            var player = _world.Entities.Player;
            if (_clock.NowMs - _saidAtMs < BotScript.DumpSettleMs || player == null)
            {
                return false;
            }

            var path = Path.Combine(_request.Directory, "move-" + player.Serial.Value.ToString("X8") + ".json");
            using (var server = ReadFresh(path))
            {
                if (server == null)
                {
                    return false;
                }

                var checker = new MovementCheck(_data.Map, _data.Tiles, _data.Multis, _world.Entities);
                var report = MoveDumpComparison.Compare(server.RootElement, checker, player.Serial.Value);
                return Report(report, path);
            }
        }

        public void Finish()
        {
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
                return null;
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
                _log.Info("Сверка физики движения с сервером: совпадает.");
                return true;
            }

            foreach (var line in report.Differences)
            {
                _log.Warning("Расхождение: " + line);
            }

            if (report.DifferenceCount > report.Differences.Count)
            {
                _log.Warning("…и ещё расхождений: " + (report.DifferenceCount - report.Differences.Count) + ".");
            }

            Failure = "физика движения расходится с сервером: " + report.DifferenceCount + " (первое — " + report.Differences[0] + "). Дамп: " + path + ".";
            return false;
        }
    }
}
