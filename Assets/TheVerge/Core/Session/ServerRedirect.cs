namespace TheVerge.Core.Session
{
    /// <summary>
    /// Куда идти после выбора шарда (PLAN Стадия 1, п. 6–7): адрес и порт игрового сервера и ключ, которым клиент
    /// открывает игровое соединение и который повторяет во входе на игровой сервер.
    /// </summary>
    public readonly struct ServerRedirect
    {
        public ServerRedirect(string host, int port, int authKey)
        {
            Host = host;
            Port = port;
            AuthKey = authKey;
        }

        public string Host { get; }

        public int Port { get; }

        public int AuthKey { get; }

        /// <summary>Ключ не печатается: по нему сервер пускает без пароля.</summary>
        public override string ToString() => Host + ":" + Port;
    }
}
