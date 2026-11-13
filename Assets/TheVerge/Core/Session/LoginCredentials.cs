using System;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Логин и пароль аккаунта. Сервер читает каждое поле как строку Latin-1 фиксированной длины
    /// (IncomingAccountPackets.cs, AccountLogin и GameLogin: ReadLatin1Safe(30)), поэтому длиннее
    /// <see cref="ServerLimits.CredentialMaxChars"/> и буквы вне Latin-1 не пропускаются: сервер получил бы другой логин.
    /// Пароль не попадает в <see cref="ToString"/>.
    /// </summary>
    public readonly struct LoginCredentials
    {
        public LoginCredentials(string username, string password)
        {
            Require(username, nameof(username));
            Require(password, nameof(password));
            Username = username;
            Password = password;
        }

        public string Username { get; }

        public string Password { get; }

        public override string ToString() => Username + " (пароль скрыт)";

        /// <summary>Что не так с логином или паролем для сервера; пусто — можно отправлять. Значения поля не содержит.</summary>
        public static string Problem(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > ServerLimits.CredentialMaxChars)
            {
                return "Поле должно быть длиной от 1 до " + ServerLimits.CredentialMaxChars + " символов.";
            }

            foreach (var c in value)
            {
                if (c > ServerLimits.MaxLatin1Char)
                {
                    return "В поле есть буква вне Latin-1: сервер прочитал бы её иначе.";
                }
            }

            return string.Empty;
        }

        private static void Require(string value, string name)
        {
            var problem = Problem(value);
            if (problem.Length > 0)
            {
                throw new ArgumentException(problem, name);
            }
        }
    }
}
