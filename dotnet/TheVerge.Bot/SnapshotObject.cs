using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// Объект в снимке мира — поля, которые клиент знает от сервера и которые сверяются с дампом сервера (PLAN Стадия 2,
    /// п. 11; формат — Custom/TheVerge/Parity/VisibleObjectsDump.cs сервера).
    /// </summary>
    public sealed class SnapshotObject
    {
        public Serial Serial { get; set; }

        /// <summary>mobile или item.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>world, equipped, container; у бота ещё unplaced — заглушка, которой у сервера нет.</summary>
        public string Place { get; set; } = string.Empty;

        public int Graphic { get; set; }

        public int Hue { get; set; }

        public int Amount { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Z { get; set; }

        /// <summary>На ком надет или в чём лежит; 0 — на земле.</summary>
        public Serial Parent { get; set; }

        public int Layer { get; set; }

        public override string ToString() =>
            Kind + " " + Place + " 0x" + Graphic.ToString("X4") + " цвет " + Hue + " ×" + Amount + " (" + X + ", " + Y + ", " + Z + ")" +
            (Parent.IsValid ? " в " + Parent : string.Empty) + (Layer != 0 ? " слой " + Layer : string.Empty);
    }
}
