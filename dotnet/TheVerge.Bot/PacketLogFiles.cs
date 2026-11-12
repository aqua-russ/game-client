using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheVerge.Bot
{
    /// <summary>
    /// Файлы лога пакетов бота: один файл на запуск, имя — дата и время запуска. Бот хранит <see cref="KeepCount"/>
    /// последних (docs/TARGET.md §3.1) и удаляет только свои файлы — по шаблону имени.
    /// </summary>
    public static class PacketLogFiles
    {
        public const int KeepCount = 20;

        private const string TimeFormat = "yyyy-MM-dd_HH-mm-ss";
        private const string Extension = ".tsv";

        private static readonly Regex OwnName = new Regex(@"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}(_\d+)?\.tsv$");

        /// <summary>Путь нового файла; папка создаётся. Если файл с этим временем уже есть — «_2», «_3»…</summary>
        public static string NewPath(string directory, DateTime started)
        {
            Directory.CreateDirectory(directory);
            var stem = started.ToString(TimeFormat, CultureInfo.InvariantCulture);
            var path = Path.Combine(directory, stem + Extension);
            for (var n = 2; File.Exists(path); n++)
            {
                path = Path.Combine(directory, stem + "_" + n + Extension);
            }

            return path;
        }

        /// <summary>Удалить свои файлы сверх <see cref="KeepCount"/> самых новых (имя — время, поэтому порядок по имени).</summary>
        public static void Prune(string directory)
        {
            var own = Directory.GetFiles(directory)
                .Where(path => OwnName.IsMatch(Path.GetFileName(path)))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Skip(KeepCount);
            foreach (var old in own)
            {
                File.Delete(old);
            }
        }
    }
}
