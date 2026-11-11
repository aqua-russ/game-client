namespace TheVerge.Core.Session
{
    /// <summary>
    /// Настройки самого клиента, которых нет на сервере: пределы, таймауты, бюджеты. Единственное место этих чисел
    /// в коде клиента; документ — docs/TARGET.md §3.1 «Настройки клиента», решение — ADR 0013.
    /// Лимиты сервера — в <see cref="ServerLimits"/>.
    /// </summary>
    public static class ClientLimits
    {
        /// <summary>
        /// Сколько байт с места ошибки попадает в дамп, когда длина пакета неизвестна (неизвестный номер, сломанная
        /// длина). Если длина известна, в дамп идёт весь пакет.
        /// </summary>
        public const int ErrorDumpBytes = 256;

        /// <summary>
        /// Предел очереди целых пакетов от сокета к главному потоку, байт. Как network.sendBufferMaxSize сервера
        /// (NetState.Network.cs): полная очередь останавливает чтение сокета, и сервер сам отключает клиента.
        /// </summary>
        public const int InboundQueueBytes = 2 * 1024 * 1024;

        /// <summary>Сколько ждать ответа сервера на подключение, мс.</summary>
        public const int ConnectTimeoutMs = 10000;
    }
}
