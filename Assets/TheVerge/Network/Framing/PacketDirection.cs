namespace TheVerge.Network.Framing
{
    /// <summary>
    /// Кто шлёт пакет. Один и тот же номер в разных направлениях — разные пакеты (0x22 от клиента — запрос
    /// ресинхронизации, от сервера — подтверждение шага), поэтому таблица пакетов разделена по направлению.
    /// </summary>
    public enum PacketDirection : byte
    {
        ClientToServer = 0,
        ServerToClient = 1,
    }
}
