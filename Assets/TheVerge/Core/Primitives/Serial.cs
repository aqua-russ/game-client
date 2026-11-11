using System;

namespace TheVerge.Core.Primitives
{
    /// <summary>
    /// 32-битный номер объекта мира: мобы, затем предметы. Границы — как у сервера: Projects/Server/World/World.cs
    /// (ItemOffset, MaxVirtualSerial). Обычные предметы сервера кончаются на 0x7EEEEEEE, выше — виртуальные
    /// (World.NewVirtual, например заклинания в книге), и клиент получает их тоже как предметы.
    /// </summary>
    public readonly struct Serial : IEquatable<Serial>, IComparable<Serial>
    {
        public const uint FirstItem = 0x40000000;
        public const uint LastItem = 0x7FFFFFFF;

        public static readonly Serial Zero = new Serial(0);

        public uint Value { get; }

        public bool IsValid => Value > 0;
        public bool IsMobile => Value > 0 && Value < FirstItem;
        public bool IsItem => Value >= FirstItem && Value <= LastItem;

        public Serial(uint value)
        {
            Value = value;
        }

        public bool Equals(Serial other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is Serial other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public int CompareTo(Serial other) => Value.CompareTo(other.Value);
        public override string ToString() => $"0x{Value:X8}";

        public static implicit operator uint(Serial serial) => serial.Value;
        public static explicit operator Serial(uint value) => new Serial(value);
        public static bool operator ==(Serial a, Serial b) => a.Value == b.Value;
        public static bool operator !=(Serial a, Serial b) => a.Value != b.Value;
    }
}
