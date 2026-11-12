namespace TheVerge.Core.Session
{
    /// <summary>
    /// Ошибка входа: вид, код сервера (для отказов) или причина разрыва, подробности для лога. Пароля здесь не бывает.
    /// </summary>
    public readonly struct LoginError
    {
        public LoginError(LoginErrorKind kind, int code, string detail)
        {
            Kind = kind;
            Code = code;
            Detail = detail;
        }

        public static LoginError None => new LoginError(LoginErrorKind.None, 0, string.Empty);

        public LoginErrorKind Kind { get; }

        /// <summary>Код сервера (<see cref="LoginRejectReason"/> …) или <see cref="DisconnectReason"/> для разрыва.</summary>
        public int Code { get; }

        public string Detail { get; }

        public override string ToString() => Kind + " (" + Code + ")" + (string.IsNullOrEmpty(Detail) ? string.Empty : ": " + Detail);
    }
}
