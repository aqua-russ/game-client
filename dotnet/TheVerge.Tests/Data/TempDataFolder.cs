using System;
using System.IO;

namespace TheVerge.Tests.Data
{
    /// <summary>
    /// Временная папка с файлами данных для тестов: файлы нужного размера (пустые, место на диске не занимают) или с
    /// заданными байтами. Удаляется в <see cref="Dispose"/>.
    /// </summary>
    internal sealed class TempDataFolder : IDisposable
    {
        public TempDataFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "theverge-data-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string File(string name) => System.IO.Path.Combine(Path, name);

        /// <summary>Файл заданной длины из нулей.</summary>
        public string Sized(string name, long length)
        {
            var path = File(name);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                stream.SetLength(length);
            }

            return path;
        }

        public string Bytes(string name, byte[] data)
        {
            var path = File(name);
            System.IO.File.WriteAllBytes(path, data);
            return path;
        }

        public void Delete(string name) => System.IO.File.Delete(File(name));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
