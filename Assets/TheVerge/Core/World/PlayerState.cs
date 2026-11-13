using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Свой персонаж при входе в мир (PLAN Стадия 1, п. 9): серийник, тело, где стоит, куда смотрит, размер карты.
    /// Дальше его ведёт модель мира (Стадия 2).
    /// </summary>
    public readonly struct PlayerState
    {
        public PlayerState(Serial serial, ushort body, Point3D position, Direction direction, MapSize map)
        {
            Serial = serial;
            Body = body;
            Position = position;
            Direction = direction;
            Map = map;
        }

        public Serial Serial { get; }

        public ushort Body { get; }

        public Point3D Position { get; }

        public Direction Direction { get; }

        public MapSize Map { get; }

        public override string ToString() => "0x" + Serial.Value.ToString("X8") + " " + Position;
    }
}
