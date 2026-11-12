namespace TheVerge.Bot
{
    /// <summary>
    /// Коды выхода бота: по ним скрипт проверки понимает, что случилось, не читая текст. Согласованы с пользователем
    /// 2026-11-12; отказы входа (0x82, 0x53) получат свои коды в задаче 1.11.
    /// </summary>
    public static class BotExitCode
    {
        /// <summary>Всё, что бот должен был сделать, сделано.</summary>
        public const int Success = 0;

        /// <summary>Неверные параметры или нет пароля.</summary>
        public const int BadArguments = 1;

        /// <summary>Не удалось подключиться: сервер не запущен, адрес неверный, сервер не ответил вовремя.</summary>
        public const int ConnectFailed = 2;

        /// <summary>Сервер закрыл соединение или ошибка сети.</summary>
        public const int ConnectionLost = 3;

        /// <summary>Сервер прислал поток, который клиент не понимает (дамп — в логе).</summary>
        public const int ProtocolError = 4;

        /// <summary>Ошибка в самом боте.</summary>
        public const int InternalError = 5;

        /// <summary>Сервер отказал во входе (неверный пароль, аккаунт заблокирован или занят); причина — в сообщении.</summary>
        public const int LoginRejected = 6;

        /// <summary>Остановлено по Ctrl+C (как принято в Unix: 128 + номер сигнала SIGINT).</summary>
        public const int Interrupted = 130;
    }
}
