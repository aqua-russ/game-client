namespace TheVerge.Network.Session
{
    /// <summary>Что стало с пакетом в <see cref="PacketDispatcher.Dispatch"/>.</summary>
    public enum DispatchResult : byte
    {
        Handled = 0,

        /// <summary>Пакет есть в матрице, но обработчика пока нет: пропущен (ADR 0013 п. 8).</summary>
        NoHandler = 1,

        /// <summary>Пакет короче, чем ждёт обработчик: в лог с дампом и пропущен (AGENTS.md §3.5).</summary>
        ReadError = 2,
    }
}
