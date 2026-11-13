using System.Collections.Generic;
using System.Text.Json;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;

namespace TheVerge.Bot
{
    /// <summary>Клетки дампа сервера против карты клиента: земля (номер, Z) и статика (число, номер, Z, цвет по порядку).</summary>
    public static class MapCellsComparison
    {
        /// <summary>Сверяет клетки; возвращает, сколько их было.</summary>
        public static int Compare(JsonElement cells, IMapData map, MapDumpReport report)
        {
            var statics = new List<StaticTile>();
            var count = 0;
            foreach (var cell in cells.EnumerateArray())
            {
                CompareCell(cell, map, statics, report);
                count++;
            }

            return count;
        }

        /// <summary>Все клетки со статикой: сверка каждой и их числа со своим (обход всей карты).</summary>
        public static void CompareAllStatics(JsonElement cells, IMapData map, MapDumpReport report)
        {
            var serverCount = Compare(cells, map, report);
            var clientCount = 0;
            var statics = new List<StaticTile>();
            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    map.TryGetStatics(x, y, statics);
                    clientCount += statics.Count > 0 ? 1 : 0;
                }
            }

            if (serverCount != clientCount)
            {
                report.Differ("клеток со статикой — у сервера " + serverCount + ", у клиента " + clientCount);
            }

            report.Summary.Add("Клетки со статикой: сервер " + serverCount + ", клиент " + clientCount + ".");
        }

        private static void CompareCell(JsonElement cell, IMapData map, List<StaticTile> statics, MapDumpReport report)
        {
            int x = cell.GetProperty("x").GetInt32(), y = cell.GetProperty("y").GetInt32();
            var where = "клетка (" + x + ", " + y + "): ";
            if (!map.TryGetLand(x, y, out var land))
            {
                report.Differ(where + "у клиента вне карты");
                return;
            }

            Field(report, where + "номер земли", cell.GetProperty("land").GetInt32(), land.Id);
            Field(report, where + "высота земли", cell.GetProperty("z").GetInt32(), land.Z);
            map.TryGetStatics(x, y, statics);
            var server = cell.GetProperty("statics");
            if (server.GetArrayLength() != statics.Count)
            {
                report.Differ(where + "статики — у сервера " + server.GetArrayLength() + ", у клиента " + statics.Count);
                return;
            }

            for (var i = 0; i < statics.Count; i++)
            {
                var tile = server[i];
                var name = where + "статика " + (i + 1) + ": ";
                Field(report, name + "номер", tile[0].GetInt32(), statics[i].Id);
                Field(report, name + "высота", tile[1].GetInt32(), statics[i].Z);
                Field(report, name + "цвет", tile[2].GetInt32(), statics[i].Hue);
            }
        }

        private static void Field(MapDumpReport report, string what, long server, long client)
        {
            if (server != client)
            {
                report.Differ(what + " — сервер " + server + ", клиент " + client);
            }
        }
    }
}
