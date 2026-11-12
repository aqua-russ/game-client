namespace TheVerge.Bot
{
    /// <summary>Итог чтения файла настроек: настройки, ошибка (бот не запускается) и предупреждение (запускается).</summary>
    public sealed class LoadedConfig
    {
        public BotSettings Settings { get; } = new BotSettings();

        public string Error { get; internal set; } = string.Empty;

        public string Warning { get; internal set; } = string.Empty;
    }
}
