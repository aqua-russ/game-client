using System.Collections.Generic;
using System.Linq;

namespace TheVerge.Bot
{
    /// <summary>Сравнение двух снимков мира по серийнику и полям, которые сверяются (PLAN Стадия 2, критерии).</summary>
    internal static class SnapshotComparison
    {
        public static List<string> Compare(WorldSnapshot bot, WorldSnapshot server)
        {
            var result = new List<string>();
            var mine = bot.Objects.ToDictionary(o => o.Serial.Value);
            var theirs = server.Objects.ToDictionary(o => o.Serial.Value);
            foreach (var s in server.Objects.OrderBy(o => o.Serial.Value))
            {
                if (!mine.TryGetValue(s.Serial.Value, out var b))
                {
                    result.Add(s.Serial + ": нет у бота (на сервере: " + s + ").");
                    continue;
                }

                CompareFields(b, s, result);
            }

            foreach (var b in bot.Objects.Where(o => !theirs.ContainsKey(o.Serial.Value)).OrderBy(o => o.Serial.Value))
            {
                result.Add(b.Serial + ": нет на сервере (у бота: " + b + ").");
            }

            return result;
        }

        private static void CompareFields(SnapshotObject bot, SnapshotObject server, List<string> result)
        {
            Field("kind", bot.Kind, server.Kind);
            Field("place", bot.Place, server.Place);
            Field("graphic", bot.Graphic.ToString(), server.Graphic.ToString());
            Field("hue", bot.Hue.ToString(), server.Hue.ToString());
            Field("amount", bot.Amount.ToString(), server.Amount.ToString());
            Field("x/y/z", bot.X + ", " + bot.Y + ", " + bot.Z, server.X + ", " + server.Y + ", " + server.Z);
            Field("parent", bot.Parent.ToString(), server.Parent.ToString());
            Field("layer", bot.Layer.ToString(), server.Layer.ToString());

            void Field(string name, string mine, string theirs)
            {
                if (mine != theirs)
                {
                    result.Add(server.Serial + ": " + name + " — у бота " + mine + ", на сервере " + theirs + ".");
                }
            }
        }
    }
}
