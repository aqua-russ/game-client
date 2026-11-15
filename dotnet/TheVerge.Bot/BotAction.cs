using System.Globalization;
using TheVerge.Core.Primitives;
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

        /// <summary>Приставка моба в прицеле: target:mob:0xТЕЛО.</summary>
        public const string MobilePrefix = "mob:";

        /// <summary>Как действие записано в командной строке — для лога.</summary>
        public string Text { get; }

        /// <summary>Разобрать «вид:значение»; null — непонятно что, причина — в <paramref name="error"/>.</summary>
        public static BotAction? Parse(string text, out string error)
        {
            var colon = text.IndexOf(':');
            var kind = colon < 0 ? text : text.Substring(0, colon);
            var argument = colon < 0 ? string.Empty : text.Substring(colon + 1);
            var parsed = WorldKind(kind, argument) ?? CombatKind(kind, argument) ?? ParityKind(kind, argument) ??
                         MovementKind(kind, argument);
            error = parsed == null ? "--action «" + text + "»: непонятное действие. Список действий: --help." : string.Empty;
            return parsed == null ? null : new BotAction(parsed.Value, argument, text);
        }

        /// <summary>Действия в мире: рюкзак, речь, ожидание клетки, прицел, ожидание предмета или моба.</summary>
        private static BotActionKind? WorldKind(string kind, string argument)
        {
            switch (kind)
            {
                case "use" when argument == "backpack":
                    return BotActionKind.UseBackpack;
                case "use" when TryParseGraphic(argument, out _):
                    return BotActionKind.UseItem;
                case "bandage" when argument == "self":
                    return BotActionKind.Bandage;
                case "say" when argument.Trim().Length > 0 && argument.Trim().Length <= ServerLimits.SpeechMaxChars:
                    return BotActionKind.Say;
                case "at" when TryParseCell(argument, out _, out _) || TryParsePoint(argument, out _):
                    return BotActionKind.WaitAt;
                case "target" when argument == "here" || argument == "self" || argument == "cancel" ||
                                   TryParsePoint(argument, out _) || TryParseGraphic(argument, out _) || TryParseMobileBody(argument, out _):
                    return BotActionKind.Target;
                case "seen" when SeenItemStep.TryParse(argument, out _, out _):
                    return BotActionKind.SeenItem;
                case "near" when TryParseGraphic(argument, out _):
                    return BotActionKind.NearItem;
                case "seenmob" when TryParseGraphic(argument, out _):
                    return BotActionKind.SeenMobile;
                default:
                    return null;
            }
        }

        /// <summary>Бой и смерть (Стадия 4, п. 7, 9).</summary>
        private static BotActionKind? CombatKind(string kind, string argument)
        {
            switch (kind)
            {
                case "fight" when TryParseGraphic(argument, out _):
                    return BotActionKind.Fight;
                case "deathof" when TryParseGraphic(argument, out _):
                    return BotActionKind.DeathOf;
                case "dead" when argument.Length == 0:
                    return BotActionKind.Dead;
                case "alive" when argument.Length == 0:
                    return BotActionKind.Alive;
                default:
                    return null;
            }
        }

        /// <summary>Сверки с сервером: мир, данные карты, состояние мобов.</summary>
        private static BotActionKind? ParityKind(string kind, string argument)
        {
            switch (kind)
            {
                case "dump" when argument.Length > 0:
                    return BotActionKind.Dump;
                case "mapdump" when MapDumpRequest.TryParse(argument, out _):
                    return BotActionKind.MapDump;
                case "movedump" when MoveDumpRequest.TryParse(argument, out _):
                    return BotActionKind.MoveDump;
                case "statusdump" when argument.Length > 0:
                    return BotActionKind.StatusDump;
                case "onmap" when TryParseMap(argument, out _):
                    return BotActionKind.OnMap;
                case "seenmulti" when argument.Length == 0:
                    return BotActionKind.SeenMulti;
                case "scene" when SceneStep.TryParseRadius(argument, out _):
                    return BotActionKind.Scene;
                case "inspect" when InspectStep.TryParseCell(argument, out _, out _, out _):
                    return BotActionKind.Inspect;
                default:
                    return null;
            }
        }

        /// <summary>Движение своего персонажа (Стадия 3).</summary>
        private static BotActionKind? MovementKind(string kind, string argument)
        {
            switch (kind)
            {
                case "walk" when WalkRequest.TryParse(argument, out _):
                    return BotActionKind.Walk;
                case "mounted" when argument.Length == 0:
                    return BotActionKind.Mounted;
                case "goto" when GotoRequest.TryParse(argument, out _):
                    return BotActionKind.Goto;
                case "blocked" when CompassDirection.TryParse(argument, out _):
                    return BotActionKind.Blocked;
                case "resync" when argument.Length == 0:
                    return BotActionKind.Resync;
                case "randomwalk" when RandomWalkRequest.TryParse(argument, out _):
                    return BotActionKind.RandomWalk;
                default:
                    return null;
            }
        }

        /// <summary>Номер карты «0»–«5» из onmap:N (видимые карты сервера — 0–5, <c>map-definitions.json</c>).</summary>
        public static bool TryParseMap(string text, out int map)
        {
            map = 0;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out map) && map <= 5;
        }

        /// <summary>Клетка с высотой «x,y,z» из target:x,y,z.</summary>
        public static bool TryParsePoint(string text, out Point3D point)
        {
            point = Point3D.Zero;
            var parts = text.Split(',');
            if (parts.Length != 3 || !TryParseCell(parts[0] + "," + parts[1], out var x, out var y) ||
                !sbyte.TryParse(parts[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var z))
            {
                return false;
            }

            point = new Point3D(x, y, z);
            return true;
        }

        /// <summary>Тело моба «mob:0x0190» из target:mob:0x0190.</summary>
        public static bool TryParseMobileBody(string text, out ushort body)
        {
            body = 0;
            return text.StartsWith(MobilePrefix, System.StringComparison.Ordinal) && TryParseGraphic(text.Substring(MobilePrefix.Length), out body);
        }

        /// <summary>Графика «0x09D0» из seen:0x09D0.</summary>
        public static bool TryParseGraphic(string text, out ushort graphic)
        {
            graphic = 0;
            return text.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase) &&
                   ushort.TryParse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out graphic);
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
