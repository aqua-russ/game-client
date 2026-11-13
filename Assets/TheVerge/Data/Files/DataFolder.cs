using System;
using System.IO;

namespace TheVerge.Data.Files
{
    /// <summary>
    /// Поиск файла в папке данных без учёта регистра имени, как у сервера (Core.FindDataFile в Linux): в установке
    /// клиента и в копиях бывают «TileData.mul» и «tiledata.mul». Возвращает настоящее имя файла с диска.
    /// </summary>
    public sealed class DataFolder
    {
        public DataFolder(string path)
        {
            Path = path;
        }

        public string Path { get; }

        /// <summary>Полный путь к файлу или null, если его нет.</summary>
        public string? Find(string name)
        {
            string? found = null;
            foreach (var file in Directory.EnumerateFiles(Path))
            {
                var fileName = System.IO.Path.GetFileName(file);
                if (string.Equals(fileName, name, StringComparison.Ordinal))
                {
                    return file;
                }

                if (found == null && string.Equals(fileName, name, StringComparison.OrdinalIgnoreCase))
                {
                    found = file;
                }
            }

            return found;
        }

        public static long Length(string path) => new FileInfo(path).Length;
    }
}
