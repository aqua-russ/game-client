namespace TheVerge.Bot
{
    /// <summary>Что задано в командной строке; null — не задано, берётся из файла настроек или по умолчанию.</summary>
    public sealed class BotOverrides
    {
        public string? Host { get; set; }

        public int? Port { get; set; }

        public string? Login { get; set; }

        public string? Character { get; set; }
    }
}
