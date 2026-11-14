using System.Text.Json;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>Вся tiledata дампа сервера против таблицы клиента: земля (флаги, имя), предметы (все поля и имя).</summary>
    public static class TileDataComparison
    {
        private static readonly string[] ItemFields = { "флаги", "вес", "качество", "анимация", "количество", "значение", "высота" };

        public static void Compare(JsonElement land, JsonElement items, ITileData tiles, ParityReport report)
        {
            var id = 0;
            foreach (var entry in land.EnumerateArray())
            {
                var mine = tiles.Land(id);
                var where = "земля 0x" + id.ToString("X4") + ": ";
                Number(report, where + "флаги", entry[0].GetUInt64(), (ulong)mine.Flags);
                Name(report, where, entry[1].GetString() ?? string.Empty, mine.Name);
                id++;
            }

            var landCount = id;
            id = 0;
            foreach (var entry in items.EnumerateArray())
            {
                CompareItem(entry, tiles, id, report);
                id++;
            }

            report.Summary.Add("Tiledata: сверено земли " + landCount + ", предметов " + id + ".");
        }

        private static void CompareItem(JsonElement entry, ITileData tiles, int id, ParityReport report)
        {
            var mine = tiles.Item(id);
            var where = "предмет 0x" + id.ToString("X4") + ": ";
            var values = new[]
            {
                (ulong)mine.Flags, (ulong)mine.Weight, (ulong)mine.Quality, (ulong)mine.Animation, (ulong)mine.Quantity,
                (ulong)mine.Value, (ulong)mine.Height,
            };
            for (var i = 0; i < values.Length; i++)
            {
                Number(report, where + ItemFields[i], entry[i].GetUInt64(), values[i]);
            }

            Name(report, where, entry[7].GetString() ?? string.Empty, mine.Name);
        }

        private static void Number(ParityReport report, string what, ulong server, ulong client)
        {
            if (server != client)
            {
                report.Differ(what + " — сервер 0x" + server.ToString("X") + ", клиент 0x" + client.ToString("X"));
            }
        }

        private static void Name(ParityReport report, string where, string server, string client)
        {
            if (server != client)
            {
                report.Differ(where + "имя — сервер «" + server + "», клиент «" + client + "»");
            }
        }
    }
}
