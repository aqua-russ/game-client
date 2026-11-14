using System.Collections.Generic;
using System.Text.Json;
using TheVerge.Core.Multis;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// Дома и корабли дампа сервера против таблицы клиента: границы и все части по порядку; номера 0..0x3FFF, которых
    /// нет у сервера, у клиента тоже должны быть пустыми (MultiData.GetComponents — по маске 0x3FFF).
    /// </summary>
    public static class MultiComparison
    {
        private const int IdCount = 0x4000;
        private static readonly string[] Bounds = { "minX", "minY", "maxX", "maxY", "centerX", "centerY", "width", "height" };
        private static readonly string[] BoundNames = { "мин. X", "мин. Y", "макс. X", "макс. Y", "центр X", "центр Y", "ширина", "высота" };
        private static readonly string[] PartNames = { "номер", "x", "y", "z", "флаги" };

        public static void Compare(JsonElement multis, IMultiData client, ParityReport report)
        {
            var serverIds = new HashSet<int>();
            foreach (var multi in multis.EnumerateArray())
            {
                var id = multi.GetProperty("id").GetInt32();
                serverIds.Add(id);
                CompareMulti(multi, client.Get(id), "дом или корабль № " + id + ": ", report);
            }

            for (var id = 0; id < IdCount; id++)
            {
                if (!serverIds.Contains(id) && !ReferenceEquals(client.Get(id), MultiComponents.Empty))
                {
                    report.Differ("дом или корабль № " + id + ": у клиента есть, у сервера нет");
                }
            }

            report.Summary.Add("Дома и корабли: сверено домов и кораблей " + serverIds.Count + ".");
        }

        private static void CompareMulti(JsonElement server, MultiComponents mine, string where, ParityReport report)
        {
            if (ReferenceEquals(mine, MultiComponents.Empty))
            {
                report.Differ(where + "у сервера есть, у клиента нет");
                return;
            }

            var bounds = new long[] { mine.MinX, mine.MinY, mine.MaxX, mine.MaxY, mine.CenterX, mine.CenterY, mine.Width, mine.Height };
            for (var i = 0; i < Bounds.Length; i++)
            {
                Field(report, where + BoundNames[i], server.GetProperty(Bounds[i]).GetInt64(), bounds[i]);
            }

            var parts = server.GetProperty("parts");
            if (parts.GetArrayLength() != mine.Components.Count)
            {
                report.Differ(where + "частей — у сервера " + parts.GetArrayLength() + ", у клиента " + mine.Components.Count);
                return;
            }

            for (var i = 0; i < mine.Components.Count; i++)
            {
                var c = mine.Components[i];
                var values = new long[] { c.ItemId, c.X, c.Y, c.Z, (long)(ulong)c.Flags };
                for (var f = 0; f < values.Length; f++)
                {
                    var server64 = f == 4 ? (long)parts[i][f].GetUInt64() : parts[i][f].GetInt64();
                    Field(report, where + "часть " + (i + 1) + ": " + PartNames[f], server64, values[f]);
                }
            }
        }

        private static void Field(ParityReport report, string what, long server, long client)
        {
            if (server != client)
            {
                report.Differ(what + " — сервер " + server + ", клиент " + client);
            }
        }
    }
}
