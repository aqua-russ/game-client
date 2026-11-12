namespace TheVerge.Network.Diagnostics
{
    /// <summary>Что записано в строке лога пакетов.</summary>
    public enum PacketLogKind : byte
    {
        Packet = 0,

        /// <summary>Соединение установлено.</summary>
        Connected = 1,

        /// <summary>Соединение закрыто; причина — в PacketLogEntry.CloseReason.</summary>
        Closed = 2,
    }
}
