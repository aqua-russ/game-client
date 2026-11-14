using System;
using System.IO;
using System.Text.Json;
using TheVerge.Core.Chat;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// Общая часть шагов сверки с GM-командами сервера (dump:, statusdump:, mapdump:, movedump:; Custom/TheVerge/Parity):
    /// сказать команду, подождать <see cref="BotScript.DumpSettleMs"/> и дождаться свежего файла
    /// &lt;папка&gt;/&lt;префикс&gt;-&lt;серийник&gt;.json. Файл, который не читается, пробуется снова в следующем тике: [DumpVisible
    /// пишет его напрямую, и он бывает недописан. Последняя ошибка чтения попадает в текст тайм-аута.
    /// </summary>
    internal sealed class ServerDumpFile
    {
        private readonly BotWorld _world;
        private readonly IClock _clock;
        private readonly string _command;
        private readonly string _directory;
        private readonly string _prefix;
        private long _saidAtMs = -1;
        private DateTime _saidAtUtc;
        private string _lastReadError = string.Empty;

        public ServerDumpFile(BotWorld world, IClock clock, string command, string directory, string prefix)
        {
            _world = world;
            _clock = clock;
            _command = command;
            _directory = directory;
            _prefix = prefix;
        }

        /// <summary>Свежий файл сервера есть — его путь; до этого сказать команду (один раз) и ждать.</summary>
        public bool TryGetFreshPath(out string path)
        {
            path = string.Empty;
            if (_saidAtMs < 0)
            {
                // Чуть раньше «сейчас»: время файла у файловой системы бывает округлено.
                _saidAtUtc = DateTime.UtcNow.AddMilliseconds(-500);
                var result = _world.Speech.Say(_command);
                _saidAtMs = result == SpeechResult.Sent || result == SpeechResult.Queued ? _clock.NowMs : -1;
                return false;
            }

            var player = _world.Entities.Player;
            if (_clock.NowMs - _saidAtMs < BotScript.DumpSettleMs || player == null)
            {
                return false;
            }

            path = Path.Combine(_directory, _prefix + "-" + player.Serial.Value.ToString("X8") + ".json");
            return File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _saidAtUtc;
        }

        public JsonDocument? TryParseJson(string path) => TryRead(path, p => JsonDocument.Parse(File.ReadAllBytes(p)));

        /// <summary>Прочитать файл; не вышло — null (попробуем в следующем тике), ошибка запоминается для тайм-аута.</summary>
        public T? TryRead<T>(string path, Func<string, T> read) where T : class
        {
            try
            {
                return read(path);
            }
            catch (JsonException e)
            {
                _lastReadError = "файл " + path + " не разобран: " + e.Message;
                return null;
            }
            catch (IOException e)
            {
                _lastReadError = "файл " + path + " не прочитан: " + e.Message;
                return null;
            }
        }

        /// <summary>Текст тайм-аута шага: что не так, команда и последняя ошибка чтения файла, если была.</summary>
        public string TimeoutProblem(string what) =>
            "сервер не записал " + what + " в " + _directory + " (нужен аккаунт GM и сервер с командой " + _command + ")." +
            (_lastReadError.Length == 0 ? string.Empty : " Последняя ошибка: " + _lastReadError + ".");
    }
}
