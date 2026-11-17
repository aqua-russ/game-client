using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement.Tooling
{
    /// <summary>
    /// Tooling / utility-only A* target cell (for bot and automated tests only; CONTROLS.md §1). Optional Z restricts
    /// arrival to a specific floor/elevation.
    /// </summary>
    public readonly struct PathGoal
    {
        public PathGoal(short x, short y, sbyte? z = null)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public short X { get; }

        public short Y { get; }

        public sbyte? Z { get; }

        public bool Matches(Point3D point) => point.X == X && point.Y == Y && (!Z.HasValue || point.Z == Z.Value);
    }
}
