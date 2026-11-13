using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>Куда и с чем входит бот: сервер, логин и пароль, имя персонажа.</summary>
    public sealed class BotLogin
    {
        public BotLogin(string host, int port, LoginCredentials credentials, string character, bool createCharacter = false)
        {
            CreateCharacter = createCharacter;
            Host = host;
            Port = port;
            Credentials = credentials;
            Character = character;
        }

        public string Host { get; }

        public int Port { get; }

        public LoginCredentials Credentials { get; }

        /// <summary>Имя персонажа; пусто — первый в списке.</summary>
        public string Character { get; }

        /// <summary>Персонажа с именем <see cref="Character"/> нет — создать его (--create-character).</summary>
        public bool CreateCharacter { get; }

        public string Endpoint => Host + ":" + Port;
    }
}
