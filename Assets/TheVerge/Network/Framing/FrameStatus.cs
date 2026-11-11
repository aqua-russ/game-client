namespace TheVerge.Network.Framing
{
    /// <summary>Результат <see cref="PacketFramer.Next"/>.</summary>
    public enum FrameStatus : byte
    {
        /// <summary>Целого пакета ещё нет: нужно больше байт.</summary>
        NeedMore = 0,

        /// <summary>Готов целый пакет.</summary>
        Packet = 1,

        /// <summary>Поток сломан: дальше не режется, нужен дисконнект с дампом (<see cref="PacketFramer.Error"/>).</summary>
        Error = 2,
    }
}
