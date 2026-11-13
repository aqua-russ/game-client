using TheVerge.Core.Ports;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Игровая сессия в мире (PLAN Стадия 1, п. 10): ping по таймеру, задержка по эху (RTT) и выход. Сервер отключает
    /// клиента, если 30 с не было обмена данными (NetState.cs, CheckAlive), поэтому в мире ping уходит чаще
    /// (<see cref="ClientLimits.PingIntervalMs"/>). Выход: запрос → ответ сервера → клиент сам закрывает соединение.
    /// </summary>
    public sealed class GameSession : ITickHandler
    {
        private readonly LoginProgress _progress;
        private readonly ISessionSender _sender;
        private readonly IServerConnector _connector;
        private readonly Ports.IClock _clock;
        private long _nextPingAt = -1;
        private long _pingSentAt = -1;
        private byte _sequence;
        private long _logoutRequestedAt;
        private bool _closeRequested;

        public GameSession(LoginProgress progress, ISessionSender sender, IServerConnector connector, Ports.IClock clock)
        {
            _progress = progress;
            _sender = sender;
            _connector = connector;
            _clock = clock;
        }

        /// <summary>Последняя измеренная задержка до сервера, мс; -1 — ещё не измерена.</summary>
        public long LastRttMs { get; private set; } = -1;

        public void Update(long now)
        {
            if (_progress.State == LoginState.InWorld)
            {
                PingIfDue(now);
            }
            else if (_progress.State == LoginState.LoggingOut && now - _logoutRequestedAt >= ClientLimits.LogoutTimeoutMs)
            {
                // Сервер не ответил на выход — закрываем сами: ждать дальше нечего.
                Close();
            }
        }

        /// <summary>Эхо ping: тот же номер, что ушёл последним, — задержка измерена; чужой номер не считается.</summary>
        public void OnPingAck(byte sequence)
        {
            if (_pingSentAt < 0 || sequence != _sequence)
            {
                return;
            }

            LastRttMs = _clock.NowMs - _pingSentAt;
            _pingSentAt = -1;
        }

        /// <summary>Выйти из игры: запрос серверу; соединение закроется по его ответу или через LogoutTimeoutMs.</summary>
        public void Logout()
        {
            _progress.Require(LoginState.InWorld, "Выход из игры");
            _sender.SendLogout();
            _logoutRequestedAt = _clock.NowMs;
            _progress.MoveTo(LoginState.LoggingOut);
        }

        public void OnLogoutAck()
        {
            if (_progress.State == LoginState.LoggingOut)
            {
                Close();
            }
        }

        // Первый ping — через интервал после входа в мир: сам вход только что был обменом данными.
        private void PingIfDue(long now)
        {
            if (_nextPingAt < 0)
            {
                _nextPingAt = now + ClientLimits.PingIntervalMs;
                return;
            }

            if (now < _nextPingAt)
            {
                return;
            }

            _sequence = unchecked((byte)(_sequence + 1));
            _sender.SendPing(_sequence);
            _pingSentAt = _clock.NowMs;
            _nextPingAt = now + ClientLimits.PingIntervalMs;
        }

        private void Close()
        {
            if (_closeRequested)
            {
                return;
            }

            _closeRequested = true;
            _connector.Disconnect();
        }
    }
}
