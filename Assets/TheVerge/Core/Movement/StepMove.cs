using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement
{
    /// <summary>Что за шаг чужого моба: откуда, куда, направление от сервера (с битом бега) и верхом ли (Layer.Mount).</summary>
    public readonly struct StepMove
    {
        public StepMove(Point3D from, Point3D to, Direction facing, bool mounted)
        {
            From = from;
            To = to;
            Facing = facing;
            IsMounted = mounted;
        }

        public Point3D From { get; }

        public Point3D To { get; }

        /// <summary>Направление шага от сервера (с битом бега), куда смотреть во время шага.</summary>
        public Direction Facing { get; }

        public bool IsMounted { get; }

        public bool IsRunning => (Facing & Direction.Running) != 0;
    }
}
