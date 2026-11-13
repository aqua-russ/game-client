using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Что сервер сообщает о мобе в одном пакете (0x78, 0x77, 0x20, 0xF3): тело, место, направление, цвет, флаги и
    /// «злость» (notoriety). Флаги — Mobile.GetPacketFlags сервера (0x01 парализован, 0x02 женщина, 0x04 яд, 0x08 желтая
    /// полоска, 0x40 боевой режим, 0x80 скрыт).
    /// </summary>
    public readonly struct MobileInfo
    {
        /// <summary>«Злости» в пакете нет (0x20, 0xF3): известное значение не меняется. У сервера значения 1–7 (Notoriety.cs).</summary>
        public const byte NotorietyUnknown = 0;

        public MobileInfo(ushort body, Point3D position, Direction direction, ushort hue, byte flags, byte notoriety)
        {
            Body = body;
            Position = position;
            Direction = direction;
            Hue = hue;
            Flags = flags;
            Notoriety = notoriety;
        }

        public ushort Body { get; }

        public Point3D Position { get; }

        public Direction Direction { get; }

        public ushort Hue { get; }

        public byte Flags { get; }

        public byte Notoriety { get; }
    }
}
