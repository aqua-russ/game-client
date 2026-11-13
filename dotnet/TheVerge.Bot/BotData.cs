using System.IO;
using TheVerge.Data.Files;
using TheVerge.Data.Map;
using TheVerge.Data.Tiles;

namespace TheVerge.Bot
{
    /// <summary>
    /// Папка файлов данных UO у бота (--data, PLAN Стадия 2): что найдено и в каком формате — в вывод при старте, затем
    /// чтение tiledata; папка или файл не подходят — ошибка словами. Карта — только Фелукка (TARGET.md §1).
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
            data = new BotWorldData(layout, tiles);
            return string.Empty;
        }
    }
}
