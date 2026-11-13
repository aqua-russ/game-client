using System.IO;
using TheVerge.Data.Files;
using TheVerge.Data.Map;

namespace TheVerge.Bot
{
    /// <summary>
    /// Папка файлов данных UO у бота (--data, PLAN Стадия 2, п. 1): что найдено и в каком формате — в вывод при старте;
    /// папка не подходит — ошибка словами. Карта — только Фелукка (TARGET.md §1).
    /// </summary>
    public static class BotData
    {
        /// <summary>Пусто — папки нет в параметрах или всё найдено; иначе — что не так.</summary>
        public static string Detect(string? folder, TextWriter output, out DataLayout? layout)
        {
            layout = null;
            if (folder == null)
            {
                return string.Empty;
            }

            var detection = DataLayoutDetector.Detect(folder, MapDefinition.Felucca, new DataOptions());
            if (!detection.IsValid)
            {
                return "файлы данных: " + detection.Error;
            }

            layout = detection.Layout!;
            foreach (var line in layout.Describe())
            {
                output.WriteLine(line);
            }

            return string.Empty;
        }
    }
}
