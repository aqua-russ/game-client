namespace TheVerge.Network.Transport
{
    /// <summary>Что пришло от транспорта в главный поток.</summary>
    public enum InboundKind : byte
    {
        /// <summary>Целый пакет.</summary>
        Packet = 0,

        /// <summary>Поток нельзя резать дальше: нужен дисконнект с дампом (AGENTS.md §3.5).</summary>
        FramingFailed = 1,

        /// <summary>Соединение закрыто: сервером, ошибкой, таймаутом или отменой (причина — InboundEvent.CloseReason).</summary>
        Closed = 2,

        /// <summary>Подключение установлено: можно отправлять.</summary>
        Connected = 3,
    }
}
