using System;
using System.IO;
using System.Text.Json;
using TheVerge.Core.Chat;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// mapdump — сверка файлов данных (--data) с сервером (PLAN Стадия 2, п. 11б): сказать [DumpMapData (команда GM,
    /// Custom/TheVerge/Parity сервера), подождать <see cref="BotScript.DumpSettleMs"/>, дождаться свежего файла
    /// &lt;папка&gt;/mapdata-&lt;серийник&gt;.json и сверить его с <see cref="MapDataComparison"/>.
    /// </summary>
    internal sealed class MapDumpStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly BotWorldData? _data;
        private readonly MapDumpRequest _request;
        private bool _said;
        private long _saidAtMs;
        private DateTime _saidAtUtc;

        public MapDumpStep(BotWorld world, ILog log, IClock clock, BotWorldData? data, MapDumpRequest request)
        {
            _world = world;
            _log = log;
            _clock = clock;
            _data = data;
            _request = request;
            Failure = data == null ? "нужна папка файлов данных: --data <папка>." : string.Empty;
        }

        public string TimeoutProblem =>
            "сервер не записал дамп карты в " + _request.Directory + " (нужен аккаунт GM и сервер с командой [DumpMapData).";

        public string Failure { get; private set; }

        public bool Poll()
        {
            if (_data == null)
            {
                return false;
            }

            if (!_said)
            {
                // Чуть раньше «сейчас»: время файла у файловой системы бывает округлено.
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

            var path = Path.Combine(_request.Directory, "mapdata-" + player.Serial.Value.ToString("X8") + ".json");
            using (var server = ReadFresh(path))
            {
                return server != null && Report(MapDataComparison.Compare(server.RootElement, _data), path);
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
                return null; // сервер ещё пишет файл — прочитаем в следующем тике
            }
            catch (IOException)
            {
                return null;
            }
        }

        private bool Report(MapDumpReport report, string path)
        {
            foreach (var line in report.Summary)
            {
                _log.Info(line);
            }

            if (report.DifferenceCount == 0)
            {
                _log.Info("Сверка карты с сервером: совпадает.");
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

            Failure = "данные карты расходятся с сервером: " + report.DifferenceCount + " (первое — " + report.Differences[0] + "). Дамп: " + path + ".";
            return false;
        }
    }
}
