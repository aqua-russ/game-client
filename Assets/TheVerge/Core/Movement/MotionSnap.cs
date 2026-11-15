using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement
{
    /// <summary>Перескок картинки чужого моба: на какую клетку, почему, сколько ждущих шагов выброшено.</summary>
    public readonly struct MotionSnap
    {
        public MotionSnap(Point3D cell, SnapReason reason, int droppedSteps, long atMs)
        {
            Cell = cell;
            Reason = reason;
            DroppedSteps = droppedSteps;
            AtMs = atMs;
        }

        public Point3D Cell { get; }

        public SnapReason Reason { get; }

        /// <summary>Сколько пришедших, но ещё не показанных шагов выброшено.</summary>
        public int DroppedSteps { get; }

        public long AtMs { get; }
    }
}
