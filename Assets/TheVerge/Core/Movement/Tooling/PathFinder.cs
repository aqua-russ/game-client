using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement.Tooling
{
    /// <summary>
    /// Tooling / utility-only A* pathfinder strictly for automated bot scenarios and unit tests (CONTROLS.md §1: player
    /// controls have no auto-walk). Evaluates every step and target elevation through <see cref="MovementCheck"/> without
    /// duplicating passability rules, and reuses pre-allocated buffers for zero steady-state allocations.
    /// </summary>
    public sealed class PathFinder
    {
        public const int DefaultMaxExpandedNodes = 8192;
        public const int DefaultMaxDistance = 256;

        private const int CardinalCost = 1000;
        private const int DiagonalCost = 1002;
        private const int TurnPenalty = 1;

        private readonly MovementCheck _check;
        private readonly int _maxExpandedNodes;
        private readonly int _maxDistance;
        private readonly PathNodeTable _table;
        private readonly PathMinHeap _heap;

        public PathFinder(MovementCheck check, int maxExpandedNodes = DefaultMaxExpandedNodes, int maxDistance = DefaultMaxDistance)
        {
            if (check == null)
            {
                throw new ArgumentNullException(nameof(check));
            }

            if (maxExpandedNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes));
            }

            if (maxDistance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance));
            }

            _check = check;
            _maxExpandedNodes = maxExpandedNodes;
            _maxDistance = maxDistance;
            int nodeCapacity = Math.Max(64, maxExpandedNodes * 4);
            _table = new PathNodeTable(nodeCapacity);
            _heap = new PathMinHeap(_table, nodeCapacity);
        }

        public PathResult FindPath(Point3D start, PathGoal goal, MoverState mover, List<PathMove> path)
        {
            path.Clear();
            if (!InBounds(start.X, start.Y) || !InBounds(goal.X, goal.Y))
            {
                return new PathResult(PathStatus.OutOfBounds, 0, 0, start);
            }

            if (goal.Matches(start))
            {
                return new PathResult(PathStatus.Found, 0, 0, start);
            }

            if (Distance(start.X, start.Y, goal.X, goal.Y) > _maxDistance)
            {
                return new PathResult(PathStatus.LimitReached, 0, 0, start);
            }

            return Search(start, goal, mover, path);
        }

        private PathResult Search(Point3D start, PathGoal goal, MoverState mover, List<PathMove> path)
        {
            _table.Clear();
            _heap.Clear();
            int startIndex = _table.TryAddOpen(start, -1, Direction.North, 0, Heuristic(start, goal));
            _heap.Push(startIndex);

            int expanded = 0;
            while (_heap.Count > 0)
            {
                int current = _heap.Pop();
                var currentPos = _table.PositionOf(current);
                if (goal.Matches(currentPos))
                {
                    Reconstruct(current, path);
                    return new PathResult(PathStatus.Found, path.Count, expanded, currentPos);
                }

                if (expanded >= _maxExpandedNodes)
                {
                    return new PathResult(PathStatus.LimitReached, 0, expanded, start);
                }

                _table.MarkClosed(current);
                expanded++;
                if (!ExpandNeighbors(current, currentPos, start, goal, mover))
                {
                    return new PathResult(PathStatus.LimitReached, 0, expanded, start);
                }
            }

            return new PathResult(PathStatus.Unreachable, 0, expanded, start);
        }

        private bool ExpandNeighbors(int current, Point3D currentPos, Point3D start, PathGoal goal, MoverState mover)
        {
            for (int i = 0; i < 8; i++)
            {
                var dir = (Direction)i;
                var step = currentPos.GetStepPoint(dir);
                if (Distance(start.X, start.Y, step.X, step.Y) > _maxDistance)
                {
                    continue;
                }

                if (!_check.CheckMovement(currentPos, dir, mover, out int nextZ))
                {
                    continue;
                }

                var nextPos = new Point3D(step.X, step.Y, (sbyte)nextZ);
                if (!TryRelaxNeighbor(current, dir, nextPos, goal))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryRelaxNeighbor(int current, Direction dir, Point3D nextPos, PathGoal goal)
        {
            int nextG = _table.GCostOf(current) + StepCost(current, dir);
            if (!_table.TryFind(nextPos, out int existing))
            {
                int added = _table.TryAddOpen(nextPos, current, dir, nextG, Heuristic(nextPos, goal));
                if (added < 0)
                {
                    return false;
                }

                _heap.Push(added);
                return true;
            }

            if (_table.StateOf(existing) == PathNodeTable.StateOpen && nextG < _table.GCostOf(existing))
            {
                _table.UpdateOpen(existing, current, dir, nextG);
                _heap.DecreaseKey(existing);
            }

            return true;
        }

        private void Reconstruct(int goalIndex, List<PathMove> path)
        {
            int count = 0;
            int cursor = goalIndex;
            while (_table.ParentOf(cursor) >= 0)
            {
                count++;
                cursor = _table.ParentOf(cursor);
            }

            for (int i = 0; i < count; i++)
            {
                path.Add(default);
            }

            cursor = goalIndex;
            for (int i = count - 1; i >= 0; i--)
            {
                int parent = _table.ParentOf(cursor);
                path[i] = new PathMove(_table.PositionOf(parent), _table.PositionOf(cursor), _table.StepOf(cursor));
                cursor = parent;
            }
        }

        private int StepCost(int parentIndex, Direction dir)
        {
            int baseCost = ((int)dir & 0x1) == 0 ? CardinalCost : DiagonalCost;
            bool turned = _table.ParentOf(parentIndex) >= 0 && _table.StepOf(parentIndex) != dir;
            return turned ? baseCost + TurnPenalty : baseCost;
        }

        private static int Heuristic(Point3D point, PathGoal goal)
        {
            int dx = Math.Abs(goal.X - point.X);
            int dy = Math.Abs(goal.Y - point.Y);
            if (dx == 0 && dy == 0 && goal.Z.HasValue && point.Z != goal.Z.Value)
            {
                return CardinalCost * 2;
            }

            int min = dx < dy ? dx : dy;
            int max = dx > dy ? dx : dy;
            return max * CardinalCost + min * (DiagonalCost - CardinalCost);
        }

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _check.MapWidth && y < _check.MapHeight;

        private static int Distance(int x0, int y0, int x1, int y1) => Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
    }
}
