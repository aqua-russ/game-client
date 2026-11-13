using System;
using System.Collections.Generic;
using TheVerge.Core.Ports;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Вход в игру — конечный автомат (PLAN Стадия 1, пп. 5–10; AGENTS.md §2.2). Номеров пакетов не знает: шлёт через
    /// порт <see cref="IAccountSender"/>, подключается через <see cref="IServerConnector"/>, а пакеты сервера приходят
    /// вызовами On… от обработчиков в Network. Сразу после подключения к логин-серверу шлёт seed и логин: подключение,
    /// которое молчит 5 с, сервер блокирует на 15 минут (NetState.Network.cs, DisconnectUnattachedSockets).
    /// Ошибка — состояние <see cref="LoginState.Failed"/> и <see cref="Error"/> (<see cref="LoginProgress"/>).
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
            Characters = new CharacterSelection(Progress, sender);
            _seed = seed;
        }

        public LoginProgress Progress { get; } = new LoginProgress();

        /// <summary>Персонажи на игровом сервере (с состояния <see cref="LoginState.AwaitingCharacterList"/>).</summary>
        public CharacterSelection Characters { get; }

        public LoginState State => Progress.State;

        public LoginError Error => Progress.Error;

        /// <summary>Вход закончился: ошибкой или закрытием по просьбе клиента.</summary>
        public bool IsFinished => Progress.IsFinished;

        public event Action<LoginState>? StateChanged
        {
            add => Progress.StateChanged += value;
            remove => Progress.StateChanged -= value;
        }

        /// <summary>Список шардов от логин-сервера; пуст, пока не пришёл.</summary>
        public IReadOnlyList<ShardEntry> Shards { get; private set; } = Array.Empty<ShardEntry>();

        /// <summary>Адрес игрового сервера и ключ; заполнен с состояния <see cref="LoginState.Redirected"/>.</summary>
        public ServerRedirect Redirect { get; private set; }

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
            switch (State)
            {
                case LoginState.ConnectingLogin:
                    _sender.SendLoginSeed(_seed, ServerLimits.ClientVersion);
                    _sender.SendAccountLogin(_credentials);
                    MoveTo(LoginState.AwaitingShardList);
                    break;
                case LoginState.ConnectingGame:
                    _sender.SendGameLogin(Redirect.AuthKey, _credentials);
                    MoveTo(LoginState.AwaitingCharacterList);
                    break;
            }
        }

        public void OnDisconnected(DisconnectReason reason, string detail)
        {
            // Логин-соединение после редиректа сервер закрывает сам, но к этому времени сессия уже перешла на игровое
            // соединение (NetworkSession.Attach), и закрытие старого сюда не приходит.
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

        /// <summary>Игровой сервер не пустил персонажа в мир (другой персонаж аккаунта уже в мире).</summary>
        public void OnCharacterRejected(CharacterRejectReason reason) =>
            Fail(new LoginError(LoginErrorKind.CharacterRejected, (int)reason, reason.ToString()));

        /// <summary>Отказ логин-сервера (байт причины — ALRReason сервера). Соединение после него закрывает сервер.</summary>
        public void OnLoginRejected(LoginRejectReason reason)
        {
            Fail(new LoginError(LoginErrorKind.LoginDenied, (int)reason, reason.ToString()));
        }

        /// <summary>Список шардов (ответ на верный логин). Пустой список — ошибка протокола: выбирать не из чего.</summary>
        public void OnShardList(IReadOnlyList<ShardEntry> shards)
        {
            if (State != LoginState.AwaitingShardList)
            {
                return;
            }

            if (shards.Count == 0)
            {
                OnMalformedPacket("логин-сервер прислал пустой список шардов");
                return;
            }

            Shards = shards;
            MoveTo(LoginState.ShardSelection);
        }

        /// <summary>
        /// Выбрать шард: <paramref name="position"/> — место в <see cref="Shards"/>, серверу уходит номер шарда. Сервер
        /// принимает выбор один раз на соединение, поэтому не в своё время или с неверным номером ничего не уходит.
        /// </summary>
        public void SelectShard(int position)
        {
            Progress.Require(LoginState.ShardSelection, "Выбор шарда");

            if (position < 0 || position >= Shards.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "В списке " + Shards.Count + " шард(ов).");
            }

            _sender.SendSelectShard(Shards[position].Index);
            MoveTo(LoginState.AwaitingRedirect);
        }

        /// <summary>
        /// Адрес игрового сервера. Логин-соединение после этого сервер закрывает сам; клиент сразу подключается к игровому
        /// серверу (новое соединение закрывает прежнее).
        /// </summary>
        public void OnRedirect(ServerRedirect redirect)
        {
            if (State != LoginState.AwaitingRedirect)
            {
                return;
            }

            Redirect = redirect;
            MoveTo(LoginState.Redirected);
            MoveTo(LoginState.ConnectingGame);
            _connector.Connect(redirect.Host, redirect.Port);
        }

        /// <summary>Пакет пришёл, но разобрать его нельзя (длины не сходятся): ждать дальше нечего — ошибка протокола.</summary>
        public void OnMalformedPacket(string detail) => Progress.FailProtocol(detail);

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

        private void Fail(LoginError error) => Progress.Fail(error);

        private void MoveTo(LoginState state) => Progress.MoveTo(state);
    }
}
