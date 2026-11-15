namespace TheVerge.Core.Movement
{
    /// <summary>Чем кончилась просьба шагнуть (<see cref="PlayerMotion.Step"/>).</summary>
    public enum StepResult : byte
    {
        /// <summary>Сервер ещё не поставил персонажа в мир: ничего не ушло.</summary>
        NotInWorld,

        /// <summary>Новое направление: персонаж повернулся на месте, запрос ушёл (как Mobile.Move сервера).</summary>
        Turned,

        /// <summary>Персонаж уже на новой клетке (предсказание), запрос ушёл и ждёт ответа сервера.</summary>
        Moved,

        /// <summary>Клетка непроходима (MovementCheck): ничего не ушло, номер шага не потрачен.</summary>
        Blocked,

        /// <summary>Без ответа сервера уже висит предел шагов (ClientLimits.MaxUnacknowledgedSteps): ничего не ушло.</summary>
        QueueFull,

        /// <summary>Клиент попросил сервер о ресинхронизации и ждёт его места (0x20): ничего не ушло.</summary>
        Resyncing
    }
}
