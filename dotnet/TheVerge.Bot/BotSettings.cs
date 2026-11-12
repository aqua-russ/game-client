namespace TheVerge.Bot
{
    /// <summary>С чем бот подключается: итог файла настроек, командной строки и значений по умолчанию.</summary>
    public sealed class BotSettings
    {
        /// <summary>Сервер стенда: docs/TARGET.md §5, порт — listeners в modernuo.json (0.0.0.0:2593).</summary>
        public const string DefaultHost = "127.0.0.1";

        public const int DefaultPort = 2593;

        public string Host { get; internal set; } = DefaultHost;

        public int Port { get; internal set; } = DefaultPort;

        public string Login { get; internal set; } = string.Empty;

        /// <summary>Никуда не печатается и не пишется в лог.</summary>
        public string Password { get; internal set; } = string.Empty;

        /// <summary>Имя персонажа; пусто — выбор решит задача 1.8.</summary>
        public string Character { get; internal set; } = string.Empty;
    }
}
