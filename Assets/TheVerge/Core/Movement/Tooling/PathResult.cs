using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement.Tooling
{
    /// <summary>
    /// Tooling / utility-only result of an A* path search (for bot and automated tests only; CONTROLS.md §1).
    /// </summary>
    public readonly struct PathResult
    {
        public PathResult(PathStatus status, int steps, int expandedNodes, Point3D reached)
        {
            Status = status;
            Steps = steps;
            ExpandedNodes = expandedNodes;
            Reached = reached;
        }

        public PathStatus Status { get; }

        public int Steps { get; }

        public int ExpandedNodes { get; }

        public Point3D Reached { get; }

        public bool IsSuccess => Status == PathStatus.Found;
    }
}
