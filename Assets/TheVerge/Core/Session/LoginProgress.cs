using System;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Где сейчас вход и чем он кончился — общее для частей входа (<see cref="LoginFlow"/>, <see cref="CharacterSelection"/>).
    /// Первая ошибка не затирается следующими: после <see cref="LoginState.Failed"/> и <see cref="LoginState.Closed"/>
    /// состояние больше не меняется.
    /// </summary>
    public sealed class LoginProgress
    {
        public LoginState State { get; private set; }

        public LoginError Error { get; private set; } = LoginError.None;

        /// <summary>Вход закончился: ошибкой или закрытием по просьбе клиента.</summary>
        public bool IsFinished => State == LoginState.Failed || State == LoginState.Closed;

        public event Action<LoginState>? StateChanged;

        public void MoveTo(LoginState state)
        {
            if (IsFinished)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }

        public void Fail(LoginError error)
        {
            if (IsFinished)
            {
                return;
            }

            Error = error;
            MoveTo(LoginState.Failed);
        }

        /// <summary>Пакет пришёл, но разобрать его нельзя (длины не сходятся): ждать дальше нечего — ошибка протокола.</summary>
        public void FailProtocol(string detail) =>
            Fail(new LoginError(LoginErrorKind.ProtocolError, (int)DisconnectReason.ProtocolError, detail));

        /// <summary>Действие игрока или бота не в своё время — ошибка вызывающего кода, серверу ничего не уходит.</summary>
        public void Require(LoginState expected, string action)
        {
            if (State != expected)
            {
                throw new InvalidOperationException(action + ": нужно состояние " + expected + ", а сейчас " + State + ".");
            }
        }
    }
}
