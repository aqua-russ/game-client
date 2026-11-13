using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Что сервер сообщает о предмете в одном пакете (0xF3, 0x2E, 0x25, 0x3C, предметы 0x78): графика, количество, место
    /// (на земле — клетка, в контейнере — точка в окне с Z = 0), цвет, флаги, «свет» (у сервера — Item.Light, у
    /// источников света это направление луча) и признак multi (дом, корабль).
    /// </summary>
    public readonly struct ItemInfo
    {
        public ItemInfo(ushort graphic, ushort amount, Point3D position, ushort hue, byte flags, byte light, bool isMulti = false)
        {
            Graphic = graphic;
            Amount = amount;
            Position = position;
            Hue = hue;
            Flags = flags;
            Light = light;
            IsMulti = isMulti;
        }

        public ushort Graphic { get; }

        public ushort Amount { get; }

        public Point3D Position { get; }

        public ushort Hue { get; }

        public byte Flags { get; }

        public byte Light { get; }

        public bool IsMulti { get; }
    }
}
