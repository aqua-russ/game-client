namespace TheVerge.Network.Transport
{
    /// <summary>Почему закрыто соединение — для типизированных ошибок клиента (PLAN Стадия 1, п. 11).</summary>
    public enum ConnectionCloseReason : byte
    {
        None = 0,

        /// <summary>Сервер не ответил за ClientLimits.ConnectTimeoutMs.</summary>
        ConnectTimeout = 1,

        /// <summary>Подключиться не удалось: порт закрыт, адрес не найден, сеть недоступна.</summary>
        ConnectFailed = 2,

        /// <summary>Отменено через CancellationToken.</summary>
        Cancelled = 3,

        /// <summary>Сервер закрыл соединение.</summary>
        ServerClosed = 4,

        /// <summary>Ошибка сокета при чтении или отправке.</summary>
        SocketError = 5,

        /// <summary>Поток от сервера нельзя резать дальше; перед этим в очереди — событие FramingFailed с дампом.</summary>
        FramingFailed = 6,
    }
}
