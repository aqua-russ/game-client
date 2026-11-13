using System;
using System.Globalization;

namespace TheVerge.Bot
{
    /// <summary>
    /// Разбор командной строки бота. Пароль здесь не принимается намеренно: команда с паролем осталась бы в истории
    /// Терминала. Пароль — в файле настроек (tools/bot/setup.sh) или скрытым вводом.
    /// </summary>
    public static class BotArguments
    {
        public const string PasswordRefused =
            "пароль в командной строке не принимается: он остался бы в истории Терминала. Сохраните его один раз " +
            "командой tools/bot/setup.sh — или бот спросит пароль сам, не показывая его на экране.";

        public static ParsedArguments Parse(string[] args)
        {
            var parsed = new ParsedArguments();
            for (var i = 0; i < args.Length && parsed.Error.Length == 0; i++)
            {
                parsed.Error = Apply(args, ref i, parsed);
            }

            return parsed;
        }

        private static string Apply(string[] args, ref int i, ParsedArguments parsed)
        {
            switch (args[i])
            {
                case "--self-check":
                    parsed.Mode = BotMode.SelfCheck;
                    return string.Empty;
                case "--help":
                case "-h":
                    parsed.Mode = BotMode.Help;
                    return string.Empty;
                case "--create-character":
                    parsed.CreateCharacter = true;
                    return string.Empty;
                case "--no-packet-log":
                    parsed.PacketLog = false;
                    return string.Empty;
                case "--password":
                    return PasswordRefused;
                case "--host":
                    return Value(args, ref i, v => parsed.Overrides.Host = v);
                case "--login":
                    return Value(args, ref i, v => parsed.Overrides.Login = v);
                case "--character":
                    return Value(args, ref i, v => parsed.Overrides.Character = v);
                case "--config":
                    return Value(args, ref i, v => parsed.ConfigPath = v);
                case "--port":
                    return ReadPort(args, ref i, parsed.Overrides);
                default:
                    return "неизвестный параметр «" + args[i] + "». Список параметров: --help.";
            }
        }

        private static string Value(string[] args, ref int i, Action<string> apply)
        {
            var name = args[i];
            if (i + 1 >= args.Length)
            {
                return "после " + name + " нужно значение.";
            }

            apply(args[++i]);
            return string.Empty;
        }

        private static string ReadPort(string[] args, ref int i, BotOverrides overrides)
        {
            var error = Value(args, ref i, v => overrides.Port = ParsePort(v));
            return error.Length == 0 && overrides.Port == null ? "--port: нужен номер порта от 1 до 65535." : error;
        }

        private static int? ParsePort(string text) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port >= 1 && port <= ushort.MaxValue
                ? port
                : (int?)null;
    }
}
