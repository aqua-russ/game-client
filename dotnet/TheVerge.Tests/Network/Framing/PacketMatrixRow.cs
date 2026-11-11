using TheVerge.Network.Framing;

namespace TheVerge.Tests.Network.Framing
{
    /// <summary>Одна строка docs/protocol/packets.csv: только то, что должна вернуть таблица пакетов.</summary>
    public sealed class PacketMatrixRow
    {
        public PacketMatrixRow(int line, byte id, ushort? subCommand, PacketDirection direction, string name,
            int? fixedLength)
        {
            Line = line;
            Id = id;
            SubCommand = subCommand;
            Direction = direction;
            Name = name;
            FixedLength = fixedLength;
        }

        public int Line { get; }
        public byte Id { get; }
        public ushort? SubCommand { get; }
        public PacketDirection Direction { get; }
        public string Name { get; }

        /// <summary>Длина целиком; null — переменная длина (var).</summary>
        public int? FixedLength { get; }

        public override string ToString()
        {
            var sub = SubCommand.HasValue ? "_0x" + SubCommand.Value.ToString("X2") : string.Empty;
            return "0x" + Id.ToString("X2") + sub + "_" + Direction + "_" + Name;
        }
    }
}
