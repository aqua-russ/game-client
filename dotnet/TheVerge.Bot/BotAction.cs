using System.Globalization;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>Одно действие бота в мире из --action «вид:значение».</summary>
    public sealed class BotAction
    {
        private BotAction(BotActionKind kind, string argument, string text)
        {
            Kind = kind;
            Argument = argument;
            Text = text;
        }

        public BotActionKind Kind { get; }

        public string Argument { get; }

        /// <summary>Как действие записано в командной строке — для лога.</summary>
        public string Text { get; }

        /// <summary>Разобрать «вид:значение»; null — непонятно что, причина — в <paramref name="error"/>.</summary>
        public static BotAction? Parse(string text, out string error)
        {
            error = string.Empty;
            var colon = text.IndexOf(':');
            var kind = colon < 0 ? text : text.Substring(0, colon);
            var argument = colon < 0 ? string.Empty : text.Substring(colon + 1);
            if (kind == "use" && argument == "backpack")
            {
                return new BotAction(BotActionKind.UseBackpack, argument, text);
            }

            if (kind == "say" && argument.Trim().Length > 0 && argument.Trim().Length <= ServerLimits.SpeechMaxChars)
            {
                return new BotAction(BotActionKind.Say, argument, text);
            }

            if (kind == "at" && TryParseCell(argument, out _, out _))
            {
                return new BotAction(BotActionKind.WaitAt, argument, text);
            }

            error = "--action «" + text + "»: непонятное действие. Список действий: --help.";
            return null;
        }

        /// <summary>Клетка «x,y» из at:x,y.</summary>
        public static bool TryParseCell(string text, out short x, out short y)
        {
            x = 0;
            y = 0;
            var parts = text.Split(',');
            return parts.Length == 2 &&
                   short.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out x) &&
                   short.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out y);
        }
    }
}
