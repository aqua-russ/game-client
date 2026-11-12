namespace TheVerge.Core.Session
{
    /// <summary>Почему соединение закончилось — глазами ядра, без подробностей сокета (PLAN Стадия 1, п. 11).</summary>
    public enum DisconnectReason : byte
    {
        /// <summary>Клиент закрыл соединение сам.</summary>
        Requested = 0,

        /// <summary>Подключиться не удалось: сервер не запущен, адрес неверный.</summary>
        ConnectFailed = 1,

        /// <summary>Сервер не ответил на подключение вовремя (ClientLimits.ConnectTimeoutMs).</summary>
        ConnectTimeout = 2,

        /// <summary>Сервер закрыл соединение.</summary>
        ServerClosed = 3,

        /// <summary>Ошибка сети при чтении или отправке.</summary>
        NetworkError = 4,

        /// <summary>Сервер прислал поток, который клиент не понимает (дамп — в логе).</summary>
        ProtocolError = 5,
    }
}
