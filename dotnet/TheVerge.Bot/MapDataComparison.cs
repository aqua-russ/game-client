using System.Text.Json;

namespace TheVerge.Bot
{
    /// <summary>
    /// Сверка файлов данных клиента с дампом сервера [DumpMapData (PLAN Стадия 2, п. 11б; формат —
    /// Custom/TheVerge/Parity/MapDataDump.cs сервера): размер карты, клетки (земля и статика), все клетки со статикой
    /// (если есть в дампе), вся tiledata, все дома и корабли. Каждое расхождение называет клетку или номер и поле.
    /// </summary>
    public static class MapDataComparison
    {
        public static MapDumpReport Compare(JsonElement server, BotWorldData data)
        {
            var report = new MapDumpReport();
            var definition = data.Layout.Map.Definition;
            int width = server.GetProperty("width").GetInt32(), height = server.GetProperty("height").GetInt32();
            if (width != data.Map.Width || height != data.Map.Height)
            {
                report.Differ("размер карты — сервер " + width + "×" + height + ", клиент " + data.Map.Width + "×" + data.Map.Height);
            }

            var map = server.GetProperty("map").GetInt32();
            if (map != definition.Index)
            {
                report.Differ("карта — сервер " + map + ", клиент " + definition.Index);
            }

            var cells = MapCellsComparison.Compare(server.GetProperty("cells"), data.Map, report);
            report.Summary.Add("Клетки: сверено клеток " + cells + " (seed " + server.GetProperty("seed").GetInt32() + ").");
            if (server.TryGetProperty("staticCells", out var staticCells))
            {
                MapCellsComparison.CompareAllStatics(staticCells, data.Map, report);
            }

            TileDataComparison.Compare(server.GetProperty("land"), server.GetProperty("items"), data.Tiles, report);
            MultiComparison.Compare(server.GetProperty("multis"), data.Multis, report);
            return report;
        }
    }
}
