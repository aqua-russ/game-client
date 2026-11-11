namespace TheVerge.Network.Framing
{
    /// <summary>
    /// Где в пакете подкоманда: смещение от начала пакета и размер (1 или 2 байта). Берётся из PacketTable, а не из
    /// чисел в коде нарезки (AGENTS.md §3.5). Значение по умолчанию — подкоманды у пакета нет.
    /// </summary>
    public readonly struct SubCommandLayout
    {
        internal SubCommandLayout(int offset, int size)
        {
            Offset = offset;
            Size = size;
        }

        public bool HasSubCommand => Size > 0;

        public int Offset { get; }

        public int Size { get; }

        /// <summary>Пакет должен быть не короче этого, чтобы в нём уместилась подкоманда.</summary>
        public int MinPacketLength => Offset + Size;
    }
}
