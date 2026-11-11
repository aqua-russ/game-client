using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Шаг, отправленный серверу и ещё не подтверждённый: номер, направление и клетка, куда клиент уже поставил
    /// персонажа (предсказание). Поворот на месте — тоже шаг с той же клеткой.
    /// </summary>
    public readonly struct PendingStep
    {
        public byte Sequence { get; }
        public Direction Direction { get; }
        public Point3D TargetPosition { get; }

        public PendingStep(byte sequence, Direction direction, Point3D targetPosition)
        {
            Sequence = sequence;
            Direction = direction;
            TargetPosition = targetPosition;
        }
    }
}
