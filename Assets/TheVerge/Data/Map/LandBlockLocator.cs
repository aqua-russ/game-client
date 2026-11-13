using System;
using System.Buffers.Binary;
using System.IO;
using TheVerge.Data.Files;

namespace TheVerge.Data.Map
{
    /// <summary>
    /// Где в файле лежат 192 байта клеток блока земли. MUL: блок за блоком по 196 байт, первые 4 — заголовок блока
    /// (TileMatrix.ReadLandBlock: (x × BlockHeight + y) × 196 + 4). UOP: тот же поток, нарезанный на куски; куски
    /// сервер упорядочивает по первым 4 байтам данных (ConvertToMapEntries), а адрес ищет, складывая их длины (FindOffset).
    /// </summary>
    public sealed class LandBlockLocator
    {
        public const int BlockSize = 196;
        public const int HeaderSize = 4;

        private readonly long[]? _chunkOffsets;
        private readonly long[]? _chunkEnds;

        private LandBlockLocator(long[]? chunkOffsets, long[]? chunkEnds)
        {
            _chunkOffsets = chunkOffsets;
            _chunkEnds = chunkEnds;
        }

        public static LandBlockLocator Mul() => new LandBlockLocator(null, null);

        public static LandBlockLocator? Uop(Stream stream, string fileName, out string error)
        {
            var entries = UopIndexReader.Read(stream, fileName, UopNames.Map, out error);
            if (entries == null)
            {
                return null;
            }

            var chunks = new UopEntry[entries.Count];
            entries.Values.CopyTo(chunks, 0);
            var order = new int[chunks.Length];
            var buffer = new byte[4];
            for (var i = 0; i < chunks.Length; i++)
            {
                stream.Position = chunks[i].Offset;
                if (StreamRead.Fully(stream, buffer) < buffer.Length)
                {
                    error = Path.GetFileName(fileName) + " повреждён: кусок карты за концом файла.";
                    return null;
                }

                order[i] = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            }

            Array.Sort(order, chunks);
            return FromChunks(chunks);
        }

        /// <summary>Адрес клеток блока в файле; −1 — блока в файле нет (у сервера это пустая земля).</summary>
        public long Offset(int block)
        {
            var offset = (long)block * BlockSize + HeaderSize;
            if (_chunkOffsets == null)
            {
                return offset;
            }

            var chunk = Array.BinarySearch(_chunkEnds!, offset);
            chunk = chunk < 0 ? ~chunk : chunk + 1;
            if (chunk >= _chunkEnds!.Length)
            {
                return -1;
            }

            var start = chunk == 0 ? 0 : _chunkEnds[chunk - 1];
            return _chunkOffsets[chunk] + (offset - start);
        }

        private static LandBlockLocator FromChunks(UopEntry[] chunks)
        {
            var offsets = new long[chunks.Length];
            var ends = new long[chunks.Length];
            long total = 0;
            for (var i = 0; i < chunks.Length; i++)
            {
                offsets[i] = chunks[i].Offset;
                total += chunks[i].Size;
                ends[i] = total;
            }

            return new LandBlockLocator(offsets, ends);
        }
    }
}
