namespace TheVerge.Core.Session
{
    /// <summary>Типизированные ошибки входа (PLAN Стадия 1, «Реакция клиента на ошибки»).</summary>
    public enum LoginErrorKind : byte
    {
        None = 0,

        /// <summary>Логин-сервер отказал; код — <see cref="LoginRejectReason"/>.</summary>
        LoginDenied = 1,

        /// <summary>Не удалось подключиться или сервер не ответил вовремя.</summary>
        ConnectFailed = 2,

        /// <summary>Соединение разорвано сервером или сетью.</summary>
        Disconnected = 3,

        /// <summary>Сервер прислал поток, который клиент не понимает.</summary>
        ProtocolError = 4,
    }
}
