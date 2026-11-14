using System.Collections.Generic;

namespace TheVerge.Bot
{
    /// <summary>Разобранная командная строка бота. <see cref="Error"/> не пуст — параметры неверны, бот не запускается.</summary>
    public sealed class ParsedArguments
    {
        public string Error { get; internal set; } = string.Empty;

        public BotMode Mode { get; internal set; } = BotMode.Run;

        public BotOverrides Overrides { get; } = new BotOverrides();

        /// <summary>Файл настроек из --config; null — файл по умолчанию (remaster/secrets/bot.json).</summary>
        public string? ConfigPath { get; internal set; }

        /// <summary>Писать лог пакетов в файл (выключается --no-packet-log).</summary>
        public bool PacketLog { get; internal set; } = true;

        /// <summary>Персонажа из --character нет — создать (--create-character).</summary>
        public bool CreateCharacter { get; internal set; }

        /// <summary>Секунд в мире перед выходом (--idle); по умолчанию 0 — выйти сразу.</summary>
        public int IdleSeconds { get; internal set; }

        /// <summary>Действия в мире по очереди (--action, можно несколько).</summary>
        public List<BotAction> Actions { get; } = new List<BotAction>();

        /// <summary>Повторять вход с нарастающей паузой (--reconnect).</summary>
        public bool Reconnect { get; internal set; }

        /// <summary>Папка файлов данных UO (--data); null — файлы не нужны.</summary>
        public string? DataFolder { get; internal set; }

        /// <summary>Запись посредника для --convert-capture.</summary>
        public string CapturePath { get; internal set; } = string.Empty;

        /// <summary>Куда записать лог пакетов для --convert-capture.</summary>
        public string CaptureLogPath { get; internal set; } = string.Empty;
    }
}
