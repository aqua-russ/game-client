using System;
using System.Buffers.Binary;
using System.IO;

namespace TheVerge.Data.Files
{
    /// <summary>
    /// Заголовок файла UOP, как его проверяет сервер (UOPFiles.ReadUOPIndexes): «MYP\0», версия (карта — 5,
    /// MultiCollection — 4), подпись 0xFD23EC43, адрес первой таблицы (8 байт). Числа — little-endian.
    /// </summary>
    public static class UopHeader
    {
        public const uint Magic = 0x50594D;
        public const uint Signature = 0xFD23EC43;
        public const int Length = 20;
        public const int MapVersion = 5;
        public const int MultiVersion = 4;

        /// <summary>Пусто — заголовок верный; иначе — что не так, словами.</summary>
        public static string Check(string path, int expectedVersion)
        {
            var header = new byte[Length];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (ReadFully(stream, header) < Length)
                {
                    return Problem(path, expectedVersion, "файл короче заголовка");
                }
            }

            var span = new ReadOnlySpan<byte>(header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(span) != Magic || BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8)) != Signature)
            {
                return Problem(path, expectedVersion, "нет подписи UOP");
            }

            var version = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4));
            return version == expectedVersion ? string.Empty : Problem(path, expectedVersion, "версия " + version);
        }

        internal static int ReadFully(Stream stream, byte[] buffer)
        {
            var total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
            {
                total += read;
            }

            return total;
        }

        private static string Problem(string path, int expectedVersion, string what) =>
            Path.GetFileName(path) + " не подходит: ждали файл UOP версии " + expectedVersion + ", а " + what +
            ". Файл повреждён или от другой версии клиента.";
    }
}
