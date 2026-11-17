using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement.Tooling
{
    /// <summary>
    /// Tooling / utility-only single step in a planned A* path (for bot and automated tests only; CONTROLS.md §1).
    /// </summary>
    public readonly struct PathMove
    {
        public PathMove(Point3D from, Point3D to, Direction direction)
        {
            From = from;
            To = to;
            Direction = direction;
        }

        public Point3D From { get; }

        public Point3D To { get; }

        public Direction Direction { get; }
    }
}
