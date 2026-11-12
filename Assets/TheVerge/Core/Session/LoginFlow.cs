using System;
using TheVerge.Core.Ports;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Вход в игру — конечный автомат (PLAN Стадия 1, пп. 5–10; AGENTS.md §2.2). Номеров пакетов не знает: шлёт через
    /// порт <see cref="IAccountSender"/>, подключается через <see cref="IServerConnector"/>, а пакеты сервера приходят
    /// вызовами On… от обработчиков в Network. Сразу после подключения к логин-серверу шлёт seed и логин: подключение,
    /// которое молчит 5 с, сервер блокирует на 15 минут (NetState.Network.cs, DisconnectUnattachedSockets).
    /// Ошибка — состояние <see cref="LoginState.Failed"/> и <see cref="Error"/>; первая ошибка не затирается следующими.
    /// </summary>
    public sealed class LoginFlow : IConnectionObserver
    {
        private readonly IAccountSender _sender;
        private readonly IServerConnector _connector;
        private readonly int _seed;
        private LoginCredentials _credentials;

        /// <param name="seed">Seed логин-сервера, не 0: нулевой сервер считает мусором и отключает (NetState.cs).</param>
        public LoginFlow(IAccountSender sender, IServerConnector connector, int seed)
        {
            if (seed == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed), "Seed 0 сервер не принимает.");
            }

            _sender = sender;
            _connector = connector;
            _seed = seed;
        }

        public LoginState State { get; private set; }

        public LoginError Error { get; private set; } = LoginError.None;

        /// <summary>Вход закончился: ошибкой или закрытием по просьбе клиента.</summary>
        public bool IsFinished => State == LoginState.Failed || State == LoginState.Closed;

        public event Action<LoginState>? StateChanged;

        public void Start(string host, int port, LoginCredentials credentials)
        {
            if (State != LoginState.Idle)
            {
                throw new InvalidOperationException("Вход уже начат (" + State + ").");
            }

            _credentials = credentials;
            MoveTo(LoginState.ConnectingLogin);
            _connector.Connect(host, port);
        }

        public void OnConnected()
        {
            if (State != LoginState.ConnectingLogin)
            {
                return;
            }

            _sender.SendLoginSeed(_seed, ServerLimits.ClientVersion);
            _sender.SendAccountLogin(_credentials);
            MoveTo(LoginState.AwaitingShardList);
        }

        public void OnDisconnected(DisconnectReason reason, string detail)
        {
            if (IsFinished)
            {
                return;
            }

            if (reason == DisconnectReason.Requested)
            {
                MoveTo(LoginState.Closed);
                return;
            }

            Fail(new LoginError(KindOf(reason), (int)reason, detail));
        }

        /// <summary>Отказ логин-сервера (байт причины — ALRReason сервера). Соединение после него закрывает сервер.</summary>
        public void OnLoginRejected(LoginRejectReason reason)
        {
            Fail(new LoginError(LoginErrorKind.LoginDenied, (int)reason, reason.ToString()));
        }

        private static LoginErrorKind KindOf(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.ConnectFailed:
                case DisconnectReason.ConnectTimeout:
                    return LoginErrorKind.ConnectFailed;
                case DisconnectReason.ProtocolError:
                    return LoginErrorKind.ProtocolError;
                default:
                    return LoginErrorKind.Disconnected;
            }
        }

        private void Fail(LoginError error)
        {
            if (IsFinished)
            {
                return;
            }

            Error = error;
            MoveTo(LoginState.Failed);
        }

        private void MoveTo(LoginState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
