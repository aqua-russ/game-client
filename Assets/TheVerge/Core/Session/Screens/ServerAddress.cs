using System.Globalization;

namespace TheVerge.Core.Session.Screens
{
    /// <summary>Адрес логин-сервера из поля «хост:порт» на экране входа.</summary>
    public readonly struct ServerAddress
    {
        private const int MaxPort = 65535;

        public ServerAddress(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }

        public int Port { get; }

        /// <summary>Разобрать «хост:порт»; пусто — разобрано, иначе — что не так, словами.</summary>
        public static string Parse(string text, out ServerAddress address)
        {
            address = default;
            var trimmed = (text ?? string.Empty).Trim();
            var colon = trimmed.LastIndexOf(':');
            var problem = "Адрес сервера — в виде хост:порт, например " + ClientLimits.DefaultServerAddress + ".";
            if (colon <= 0 || colon == trimmed.Length - 1)
            {
                return problem;
            }

            var host = trimmed.Substring(0, colon);
            if (!int.TryParse(trimmed.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
                port < 1 || port > MaxPort || HasWhitespace(host))
            {
                return problem;
            }

            address = new ServerAddress(host, port);
            return string.Empty;
        }

        public override string ToString() => Host + ":" + Port.ToString(CultureInfo.InvariantCulture);

        private static bool HasWhitespace(string value)
        {
            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
