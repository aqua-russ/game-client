using System;
using System.Collections.Generic;

namespace TheVerge.Core.Session.Screens
{
    /// <summary>
    /// Экраны входа без Unity (PLAN Стадия 1, п. 16): какой экран показан, что в строке статуса, что делает каждая кнопка.
    /// Каждое «Войти» — новый клиент (<see cref="LoginParts"/>), собранный снаружи; решения о протоколе — в ядре
    /// (<see cref="LoginFlow"/>, <see cref="GameSession"/>, <see cref="Reconnector"/>), экраны их только вызывают.
    /// <see cref="Tick"/> — раз в кадр: тик ядра с бюджетом времени, затем проверка, не кончился ли вход.
    /// </summary>
    public sealed class LoginScreens : IDisposable
    {
        private readonly Func<LoginParts> _newClient;
        private LoginParts? _parts;
        private string _endpoint = string.Empty;
        private bool _creating;
        private int _revision;
        private LoginErrorKind _retryKind;
        private int _retryAttempt;
        private int _retryDelayMs;
        private long _shownSecond = -1;
        private long _shownRtt = long.MinValue;

        /// <param name="newClient">Собрать новый клиент (сеть, вход, сессия, повтор, ядро) для одной попытки входа.</param>
        public LoginScreens(Func<LoginParts> newClient)
        {
            _newClient = newClient;
        }

        public LoginForm Form { get; } = new LoginForm();

        public CharacterList Characters { get; } = new CharacterList();

        public CharacterForm Creation { get; } = new CharacterForm();

        /// <summary>Итог прошлого входа на экране входа: причина ошибки или «вы вышли»; пусто — нечего сказать.</summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>Строка статуса в мире: имя, RTT, время в мире; меняется не чаще раза в секунду.</summary>
        public string WorldStatus { get; private set; } = string.Empty;

        /// <summary>Растёт, когда меняется что-то видимое: экрану пора перерисоваться (иначе кадр ничего не делает).</summary>
        public int Revision => _revision + Form.Revision + Creation.Revision + Characters.Revision;

        public IReadOnlyList<ShardEntry> Shards => _parts?.Flow.Shards ?? Array.Empty<ShardEntry>();

        /// <summary>Вход начат: экран может запомнить адрес и логин (пароль не запоминается).</summary>
        public event Action? Submitted;

        public LoginScreen Screen
        {
            get
            {
                if (_parts == null)
                {
                    return LoginScreen.Login;
                }

                switch (_parts.Flow.State)
                {
                    case LoginState.ShardSelection:
                        return LoginScreen.Shards;
                    case LoginState.CharacterSelection:
                        return _creating ? LoginScreen.CreateCharacter : LoginScreen.Characters;
                    case LoginState.InWorld:
                        return LoginScreen.InWorld;
                    case LoginState.Closed:
                        return LoginScreen.Login;
                    case LoginState.Failed:
                        return _parts.Reconnector.WillRetry ? LoginScreen.Waiting : LoginScreen.Login;
                    default:
                        return LoginScreen.Waiting;
                }
            }
        }

        /// <summary>Что сейчас ждём (подключение, вход в мир, повтор «попытка N из 5»); пусто — ничего не ждём.</summary>
        public string Status
        {
            get
            {
                if (_parts == null)
                {
                    return string.Empty;
                }

                var state = _parts.Flow.State;
                if (state == LoginState.Failed)
                {
                    return _parts.Reconnector.WillRetry
                        ? LoginStatusText.RetryScheduled(_retryKind, _retryAttempt, _retryDelayMs)
                        : string.Empty;
                }

                var waiting = LoginStatusText.For(state, _endpoint);
                var attempt = _parts.Reconnector.Attempts;
                return attempt > 0 && state != LoginState.InWorld ? LoginStatusText.Retrying(attempt, waiting) : waiting;
            }
        }

        /// <summary>«Войти»: новый клиент с адресом, логином и паролем из формы. С неверной формой ничего не делает.</summary>
        public void Submit()
        {
            if (Screen != LoginScreen.Login || !Form.CanSubmit)
            {
                return;
            }

            Close();
            var address = Form.ServerAddress;
            var credentials = Form.Credentials;
            _endpoint = address.ToString();
            Message = string.Empty;
            Attach(_newClient());
            _parts!.Flow.Start(address.Host, address.Port, credentials);
            Submitted?.Invoke();
        }

        /// <summary>«Отмена» до входа в мир (и во время паузы перед повтором): клиент закрыт, снова экран входа.</summary>
        public void Cancel()
        {
            if (_parts == null || Screen == LoginScreen.InWorld)
            {
                return;
            }

            Close();
            Message = "Вход отменён.";
        }

        public void ChooseShard(int position)
        {
            if (Screen == LoginScreen.Shards && position >= 0 && position < Shards.Count)
            {
                _parts!.Flow.SelectShard(position);
            }
        }

        public void OpenCreation()
        {
            if (Screen != LoginScreen.Characters || !Characters.CanCreate)
            {
                return;
            }

            Creation.Reset();
            Creation.SetCities(_parts!.Flow.Characters.Cities);
            _creating = true;
            _revision++;
        }

        public void CloseCreation()
        {
            if (Screen == LoginScreen.CreateCharacter)
            {
                _creating = false;
                _revision++;
            }
        }

        /// <summary>«Создать»: только когда выбор проходит правила сервера; дальше — вход в мир новым персонажем.</summary>
        public void Create()
        {
            if (Screen == LoginScreen.CreateCharacter && Creation.CanCreate && Characters.CanCreate)
            {
                _parts!.Flow.Characters.Create(Creation.ToNewCharacter());
            }
        }

        /// <summary>«Выйти» из мира: запрос серверу, соединение закроется по его ответу (GameSession).</summary>
        public void Logout()
        {
            if (Screen == LoginScreen.InWorld)
            {
                _parts!.Game.Logout();
            }
        }

        /// <summary>Раз в кадр: тик ядра (разбор пакетов с бюджетом, таймеры, отправка), затем экран по итогам.</summary>
        public void Tick(long now)
        {
            if (_parts == null)
            {
                return;
            }

            _parts.Core.Tick(now);
            if (_parts.Flow.IsFinished && !_parts.Reconnector.WillRetry)
            {
                Finish(now);
                return;
            }

            UpdateWorldStatus(now);
        }

        public void Dispose() => Close();

        private void Finish(long now)
        {
            var flow = _parts!.Flow;
            Message = flow.State == LoginState.Closed
                ? "Вы вышли из игры."
                : LoginErrorText.Describe(flow.Error, _parts.Game.MsInWorld(now));
            Close();
        }

        private void UpdateWorldStatus(long now)
        {
            if (_parts!.Flow.State != LoginState.InWorld)
            {
                SetWorldStatus(string.Empty, -1, long.MinValue);
                return;
            }

            var ms = _parts.Game.MsInWorld(now);
            var rtt = _parts.Game.LastRttMs;
            if (ms / 1000 != _shownSecond || rtt != _shownRtt)
            {
                SetWorldStatus(LoginStatusText.InWorld(_parts.Flow.Characters.SelectedName, rtt, ms), ms / 1000, rtt);
            }
        }

        private void SetWorldStatus(string text, long second, long rtt)
        {
            _shownSecond = second;
            _shownRtt = rtt;
            if (WorldStatus != text)
            {
                WorldStatus = text;
                _revision++;
            }
        }

        private void Attach(LoginParts parts)
        {
            _parts = parts;
            parts.Flow.StateChanged += OnStateChanged;
            parts.Reconnector.RetryScheduled += OnRetryScheduled;
            Characters.Attach(parts.Flow);
            _revision++;
        }

        private void Close()
        {
            if (_parts == null)
            {
                return;
            }

            _parts.Flow.StateChanged -= OnStateChanged;
            _parts.Reconnector.RetryScheduled -= OnRetryScheduled;
            Characters.Detach();
            _parts.Dispose();
            _parts = null;
            _creating = false;
            SetWorldStatus(string.Empty, -1, long.MinValue);
            _revision++;
        }

        private void OnStateChanged(LoginState state)
        {
            if (state == LoginState.CharacterSelection)
            {
                Characters.Clear();
            }
            else
            {
                _creating = false;
            }

            _revision++;
        }

        private void OnRetryScheduled(int attempt, int delayMs)
        {
            _retryKind = _parts!.Flow.Error.Kind;
            _retryAttempt = attempt;
            _retryDelayMs = delayMs;
            _revision++;
        }
    }
}
