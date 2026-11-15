namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Почему картинка моба встала на клетку сразу, без езды (ADR 0020 пп. 3, 5). Чужие — <see cref="OthersMotion"/>,
    /// свой персонаж — <see cref="PlayerStepShow"/>.
    /// </summary>
    public enum SnapReason
    {
        /// <summary>Моб появился: первый 0x78 (или 0x77 неизвестного моба); свой персонаж — вход в мир (0x1B).</summary>
        Appeared,

        /// <summary>Телепорт: 0x78 известного моба с другой клеткой (Mobile.SetLocation с isTeleport сервера).</summary>
        Teleported,

        /// <summary>Рывок: 0x77 увёл моба дальше <see cref="Session.ClientLimits.StepSnapGapCells"/> клеток от прежней клетки сервера.</summary>
        Jumped,

        /// <summary>Картинка отстала больше <see cref="Session.ClientLimits.StepBacklogSnapMs"/>: ждущие шаги выброшены.</summary>
        Backlog,

        /// <summary>Свой персонаж: откат — сервер отказал в шаге (0x21) и поставил на свою клетку.</summary>
        Rejected,

        /// <summary>Свой персонаж: сервер сам поставил на клетку (0x20 — телепорт, ресинхронизация, смена карты).</summary>
        Repositioned
    }
}
