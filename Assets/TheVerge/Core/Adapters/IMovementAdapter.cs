using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Фасад управления перемещением персонажа по клеткам (пакеты 0x02, 0x21, 0x22).
    /// </summary>
    public interface IMovementAdapter
    {
        Point3D CurrentPosition { get; }
        Direction CurrentDirection { get; }

        /// <summary>
        /// Отправляет серверу запрос на совершение одного шага с автоматическим присвоением Sequence (0..255).
        /// Возвращает false, если превышен лимит неподтверждённых шагов (Fastwalk guard).
        /// </summary>
        bool TryRequestMove(Direction direction, bool isRunning);

        /// <summary>
        /// Поворот на месте без совершения шага.
        /// </summary>
        void RequestTurn(Direction direction);

        /// <summary> Шаг подтверждён сервером (пакет 0x22) </summary>
        event Action<MoveResultDto> OnMoveAccepted;

        /// <summary> Шаг отклонён сервером (пакет 0x21) — мгновенный возврат в координаты сервера </summary>
        event Action<MoveResultDto> OnMoveRejected;
    }
}
