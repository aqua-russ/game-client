using System;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Повтор входа с нарастающей паузой (PLAN Стадия 1, «Реакция клиента на ошибки»): не удалось подключиться или
    /// соединение разорвано в мире — новая попытка через <see cref="ClientLimits.ReconnectDelaysMs"/>, тем же шардом и
    /// персонажем; попытки кончились — ошибка остаётся. Отказы сервера (пароль, персонаж) и ошибки протокола не повторяются:
    /// повтор дал бы то же самое.
    /// </summary>
    public sealed class Reconnector : ITickHandler
    {
        private readonly LoginFlow _flow;
        private bool _wasInWorld;
        private bool _failureHandled;
        private bool _reentering;
        private long _retryAt = -1;
        private int _shard = -1;
        private string _character = string.Empty;

        public Reconnector(LoginFlow flow)
        {
            _flow = flow;
        }

        /// <summary>Сколько попыток подряд уже сделано; сбрасывается, когда персонаж снова в мире.</summary>
        public int Attempts { get; private set; }

        /// <summary>Вход упал, но будет повторён: ждать, а не заканчивать.</summary>
        public bool WillRetry { get; private set; }

        /// <summary>Повтор назначен: номер попытки и пауза, мс.</summary>
        public event Action<int, int>? RetryScheduled;

        public void Update(long now)
        {
            var state = _flow.State;
            if (state != LoginState.Failed)
            {
                _failureHandled = false;
            }

            if (state == LoginState.InWorld)
            {
                _wasInWorld = true;
                _reentering = false;
                Attempts = 0;
            }

            if (_retryAt >= 0)
            {
                RetryWhenDue(now);
            }
            else if (state == LoginState.Failed && !_failureHandled)
            {
                _failureHandled = true;
                ScheduleIfRetryable(now);
            }
            else if (_reentering)
            {
                ReselectPrevious(state);
            }
        }

        private void ScheduleIfRetryable(long now)
        {
            var kind = _flow.Error.Kind;
            var retryable = kind == LoginErrorKind.ConnectFailed ||
                            (_wasInWorld && (kind == LoginErrorKind.Disconnected || kind == LoginErrorKind.Timeout));
            if (!retryable || Attempts >= ClientLimits.ReconnectDelaysMs.Length)
            {
                return;
            }

            var delay = ClientLimits.ReconnectDelaysMs[Attempts];
            _retryAt = now + delay;
            WillRetry = true;
            RetryScheduled?.Invoke(Attempts + 1, delay);
        }

        private void RetryWhenDue(long now)
        {
            if (now < _retryAt)
            {
                return;
            }

            _retryAt = -1;
            WillRetry = false;
            _failureHandled = false; // следующая неудача этой попытки — новая
            Attempts++;
            _reentering = true;
            _shard = _flow.SelectedShard;
            _character = _flow.Characters.SelectedName;
            _flow.Restart();
        }

        // Тем же шардом и персонажем, что и до разрыва; если их больше нет в списках — выбирает игрок (или бот).
        private void ReselectPrevious(LoginState state)
        {
            if (state == LoginState.ShardSelection && _shard >= 0 && _shard < _flow.Shards.Count)
            {
                _flow.SelectShard(_shard);
            }
            else if (state == LoginState.CharacterSelection && _character.Length > 0)
            {
                var slot = _flow.Characters.FindSlot(_character);
                if (slot >= 0)
                {
                    _flow.Characters.Select(slot);
                }
            }
        }
    }
}
