using System;

namespace TheVerge.Core.Primitives
{
    /// <summary>
    /// Клетка мира UO: X, Y и высота Z. В пакетах Z — один байт со знаком (кроме 0x1B, где два байта).
    /// </summary>
    public readonly struct Point3D : IEquatable<Point3D>
    {
        public static readonly Point3D Zero = new Point3D(0, 0, 0);

        public short X { get; }
        public short Y { get; }
        public sbyte Z { get; }

        public Point3D(short x, short y, sbyte z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Соседняя клетка в направлении шага; флаг бега не влияет. Высоту не меняет: её считает проверка шага.
        /// Сдвиги — как у сервера: Projects/Server/Mobiles/Movement.cs, Movement.Offset.
        /// </summary>
        public Point3D GetStepPoint(Direction direction)
        {
            short dx = 0;
            short dy = 0;
            switch (direction & Direction.FacingMask)
            {
                case Direction.North: dy = -1; break;
                case Direction.Right: dx = 1; dy = -1; break;
                case Direction.East:  dx = 1; break;
                case Direction.Down:  dx = 1; dy = 1; break;
                case Direction.South: dy = 1; break;
                case Direction.Left:  dx = -1; dy = 1; break;
                case Direction.West:  dx = -1; break;
                case Direction.Up:    dx = -1; dy = -1; break;
            }
            return new Point3D((short)(X + dx), (short)(Y + dy), Z);
        }

        public bool Equals(Point3D other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is Point3D other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";

        public static bool operator ==(Point3D a, Point3D b) => a.Equals(b);
        public static bool operator !=(Point3D a, Point3D b) => !a.Equals(b);
    }
}
