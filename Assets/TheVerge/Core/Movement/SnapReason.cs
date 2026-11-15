namespace TheVerge.Core.Movement
{
    /// <summary>Почему картинка чужого моба встала на клетку сразу, без езды (ADR 0020 п. 5).</summary>
    public enum SnapReason
    {
        /// <summary>Моб появился: первый 0x78 (или 0x77 неизвестного моба).</summary>
        Appeared,

        /// <summary>Телепорт: 0x78 известного моба с другой клеткой (Mobile.SetLocation с isTeleport сервера).</summary>
        Teleported,

        /// <summary>Рывок: 0x77 увёл моба дальше <see cref="Session.ClientLimits.StepSnapGapCells"/> клеток от прежней клетки сервера.</summary>
        Jumped,

        /// <summary>Картинка отстала больше <see cref="Session.ClientLimits.StepBacklogSnapMs"/>: ждущие шаги выброшены.</summary>
        Backlog
    }
}
