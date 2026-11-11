using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Уникальный 32-битный идентификатор сущностей Ultima Online (персонажи, монстры, предметы, контейнеры).
    /// </summary>
    public readonly struct Serial : IEquatable<Serial>, IComparable<Serial>
    {
        public static readonly Serial Zero = new Serial(0);

        public uint Value { get; }

        public bool IsValid => Value > 0;
        public bool IsMobile => Value > 0 && Value < 0x40000000;
        public bool IsItem => Value >= 0x40000000 && Value < 0x80000000;

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
