using System.IO;

namespace TheVerge.Data.Files
{
    /// <summary>Чтение из файла «сколько есть»: поток может отдать меньше за раз, а у конца файла — меньше совсем.</summary>
    public static class StreamRead
    {
        public static int Fully(Stream stream, byte[] buffer) => Fully(stream, buffer, buffer.Length);

        /// <summary>Читает до <paramref name="count"/> байт, пока файл не кончится; возвращает, сколько прочитано.</summary>
        public static int Fully(Stream stream, byte[] buffer, int count)
        {
            var total = 0;
            int read;
            while (total < count && (read = stream.Read(buffer, total, count - total)) > 0)
            {
                total += read;
            }

            return total;
        }
    }
}
