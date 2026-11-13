namespace TheVerge.Core.Session
{
    /// <summary>
    /// Клиент никогда не ждёт молча (PLAN Стадия 1, п. 11): если вход ждёт ответа сервера дольше
    /// <see cref="ClientLimits.ServerResponseTimeoutMs"/>, — ошибка <see cref="LoginErrorKind.Timeout"/>. Пока выбирает
    /// человек (шард, персонаж) или идёт подключение (у него свой таймаут), время не считается.
    /// </summary>
    public sealed class LoginWatchdog : ITickHandler
    {
        private readonly LoginProgress _progress;
        private LoginState _seen = LoginState.Idle;
        private long _since;

        public LoginWatchdog(LoginProgress progress)
        {
            _progress = progress;
        }

        public void Update(long now)
        {
            var state = _progress.State;
            if (state != _seen)
            {
                _seen = state;
                _since = now;
                return;
            }

            if (AwaitsServer(state) && now - _since >= ClientLimits.ServerResponseTimeoutMs)
            {
                _progress.Fail(new LoginError(LoginErrorKind.Timeout, (int)state,
                    "сервер не ответил за " + ClientLimits.ServerResponseTimeoutMs / 1000 + " с (ждали: " + state + ")"));
            }
        }

        private static bool AwaitsServer(LoginState state)
        {
            switch (state)
            {
                case LoginState.AwaitingShardList:
                case LoginState.AwaitingRedirect:
                case LoginState.AwaitingCharacterList:
                case LoginState.EnteringWorld:
                case LoginState.WorldLoading:
                    return true;
                default:
                    return false;
            }
        }
    }
}
