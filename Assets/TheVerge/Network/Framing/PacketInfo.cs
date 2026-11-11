namespace TheVerge.Network.Framing
{
    /// <summary>
    /// Что известно о пакете из матрицы docs/protocol/packets.csv: длина и имя. Значение по умолчанию —
    /// «неизвестен»: такого номера или подкоманды в матрице нет, и нарезка по нему делает контролируемый
    /// дисконнект с дампом (AGENTS.md §3.5), а не угадывает длину.
    /// </summary>
    public readonly struct PacketInfo
    {
        /// <summary>Пакет переменной длины: после номера идут 2 байта длины всего пакета.</summary>
        internal const int VariableLength = -1;

        /// <summary>
        /// Где у пакета переменной длины поле длины: сразу после номера, 2 байта big-endian, длина всего пакета
        /// (Projects/Server/Network/PacketUtilities.cs, WritePacketLength).
        /// </summary>
        public const int LengthOffset = 1;

        /// <summary>Заголовок пакета переменной длины: номер и длина. Короче сервер и сам считает ошибкой (NetState.cs, HandlePacket).</summary>
        public const int VariableHeaderLength = 3;

        /// <summary>
        /// Самый длинный пакет: столько вмещает поле длины в 2 байта. Отдельного предела у сервера нет
        /// (ADR 0013, docs/TARGET.md §3.1).
        /// </summary>
        public const int MaxLength = ushort.MaxValue;

        private readonly int _length;
        private readonly string? _name;

        internal PacketInfo(int length, string name)
        {
            _length = length;
            _name = name;
        }

        /// <summary>Пакет есть в матрице.</summary>
        public bool IsKnown => _length != 0;

        /// <summary>Длину пакета нужно прочитать из 2 байт после номера.</summary>
        public bool IsVariable => _length == VariableLength;

        /// <summary>Длина пакета целиком вместе с номером; 0 — у пакета переменной длины и у неизвестного.</summary>
        public int FixedLength => _length > 0 ? _length : 0;

        /// <summary>Имя пакета из матрицы (для логов и дампов); пустая строка у неизвестного.</summary>
        public string Name => _name ?? string.Empty;
    }
}
