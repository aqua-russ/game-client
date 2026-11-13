using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace TheVerge.Data.Files
{
    /// <summary>
    /// Распаковка zlib (RFC 1950): 2 байта заголовка, поток deflate, Adler-32 в конце (big-endian). Так сжаты куски
    /// MultiCollection.uop; сервер распаковывает их libdeflate и требует точную длину (MultiData.LoadUOP). В
    /// netstandard2.1 нет ZLibStream — заголовок и контрольная сумма проверяются здесь, сам deflate — DeflateStream.
    /// </summary>
    public static class Zlib
    {
        private const int HeaderSize = 2;
        private const int TrailerSize = 4;

        /// <summary>Распакованные данные ровно <paramref name="length"/> байт или null и что не так.</summary>
        public static byte[]? Inflate(byte[] stored, int length, out string error)
        {
            if (stored.Length < HeaderSize + TrailerSize || (stored[0] & 0x0F) != 8 || (stored[0] << 8 | stored[1]) % 31 != 0 ||
                (stored[1] & 0x20) != 0)
            {
                error = "кусок не в формате zlib";
                return null;
            }

            var output = new byte[length + 1];
            int read;
            try
            {
                using (var deflate = new DeflateStream(new MemoryStream(stored, HeaderSize, stored.Length - HeaderSize), CompressionMode.Decompress))
                {
                    read = StreamRead.Fully(deflate, output);
                }
            }
            catch (InvalidDataException)
            {
                error = "сжатые данные испорчены";
                return null;
            }

            var expected = BinaryPrimitives.ReadUInt32BigEndian(stored.AsSpan(stored.Length - TrailerSize));
            error = read != length ? "длина после распаковки " + read + ", а в таблице " + length
                : Adler32(output, length) != expected ? "не сходится контрольная сумма" : string.Empty;
            if (error.Length > 0)
            {
                return null;
            }

            Array.Resize(ref output, length);
            return output;
        }

        private static uint Adler32(byte[] data, int length)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            for (var i = 0; i < length; i++)
            {
                a = (a + data[i]) % Mod;
                b = (b + a) % Mod;
            }

            return (b << 16) | a;
        }
    }
}
