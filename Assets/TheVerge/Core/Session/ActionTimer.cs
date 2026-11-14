namespace TheVerge.Core.Session
{
    /// <summary>
    /// Общая задержка действий игрока (<see cref="ServerLimits.ActionDelayMs"/>): двойной клик (IncomingEntityPackets.UseReq)
    /// и быстрый бинт (IncomingExtendedCommandPackets.BandageTarget) сервер ограничивает одной Mobile.NextActionTime. Раньше
    /// времени сервер действие не выполняет, поэтому клиент его не шлёт. Один таймер на персонажа.
    /// </summary>
    public sealed class ActionTimer
    {
        private bool _started;
        private long _lastMs;

        /// <summary>true — задержка прошла, действие начато (отсчёт заново); false — рано, ничего не меняется.</summary>
        public bool TryStart(long now)
        {
            if (_started && now - _lastMs < ServerLimits.ActionDelayMs)
            {
                return false;
            }

            _started = true;
            _lastMs = now;
            return true;
        }
    }
}
