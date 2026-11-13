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
    }
}
