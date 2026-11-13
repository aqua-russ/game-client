using System.Collections.Generic;
using System.IO;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;
using TheVerge.Data.Map;
using TheVerge.Data.Tiles;

namespace TheVerge.Bot
{
    /// <summary>
    /// Папка файлов данных UO у бота (--data, PLAN Стадия 2): что найдено и в каком формате — в вывод при старте, затем
    /// чтение tiledata и открытие карты; папка или файл не подходят — ошибка словами. Карта — только Фелукка (TARGET.md §1).
    /// </summary>
    public static class BotData
    {
        /// <summary>Пусто — папки нет в параметрах или всё прочитано; иначе — что не так.</summary>
        public static string Load(string? folder, TextWriter output, out BotWorldData? data)
        {
            data = null;
            if (folder == null)
            {
                return string.Empty;
            }

            var detection = DataLayoutDetector.Detect(folder, MapDefinition.Felucca, new DataOptions());
            if (!detection.IsValid)
            {
                return "файлы данных: " + detection.Error;
            }

            var layout = detection.Layout!;
            foreach (var line in layout.Describe())
            {
                output.WriteLine(line);
            }

            var tiles = TileDataReader.Load(layout.TileData, out var error);
            if (tiles == null)
            {
                return "файлы данных: " + error;
            }

            output.WriteLine("Tiledata прочитана: земли 0x" + TileDataTable.LandTableSize.ToString("X") + ", предметов 0x" +
                             tiles.ItemCount.ToString("X"));
            var map = MapData.Open(layout.Map, MapData.DefaultCacheBlocks, out error);
            if (map == null)
            {
                return "файлы данных: " + error;
            }

            data = new BotWorldData(layout, tiles, map);
            output.WriteLine(DescribeCenter(map));
            return string.Empty;
        }

        private static string DescribeCenter(MapData map)
        {
            int x = map.Width / 2, y = map.Height / 2;
            map.TryGetLand(x, y, out var land);
            var statics = new List<StaticTile>();
            map.TryGetStatics(x, y, statics);
            return "Карта открыта: клетка (" + x + ", " + y + ") — земля 0x" + land.Id.ToString("X4") + ", Z " + land.Z +
                   ", статики: " + statics.Count;
        }
    }
}
