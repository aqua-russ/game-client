using System;
using TheVerge.Data.Map;
using TheVerge.Tests.Data.Files;

namespace TheVerge.Tests.Data.Map
{
    /// <summary>
    /// Карта в раскладке клиента UO, как её читает сервер (TileMatrix.cs, ReadLandBlock): блоки 8×8 по столбцам
    /// (блок (bx, by) — номер bx × BlockHeight + by), в блоке 4 байта заголовка (у клиента UO — номер блока) и 64 клетки
    /// по 3 байта: номер земли (short) и Z (sbyte), клетка (x, y) блока — под номером y × 8 + x.
    /// </summary>
    internal sealed class MapFileBuilder
    {
        public const int BlockSize = 196;

        public MapFileBuilder(MapDefinition map, int seed)
        {
            Map = map;
            Mul = new byte[map.BlockCount * BlockSize];
            new Random(seed).NextBytes(Mul);
            for (var block = 0; block < map.BlockCount; block++)
            {
                BitConverter.GetBytes(block).CopyTo(Mul, block * BlockSize);
            }
        }

        public MapDefinition Map { get; }
        public byte[] Mul { get; }

        public short Id(int x, int y) => BitConverter.ToInt16(Mul, Cell(x, y));

        public sbyte Z(int x, int y) => (sbyte)Mul[Cell(x, y) + 2];

        public void Set(int x, int y, short id, sbyte z)
        {
            BitConverter.GetBytes(id).CopyTo(Mul, Cell(x, y));
            Mul[Cell(x, y) + 2] = (byte)z;
        }

        /// <summary>
        /// UOP: MUL, нарезанный на куски по <paramref name="blocksPerChunk"/> блоков. Имя куска — номер из
        /// <paramref name="nameIndex"/> (по умолчанию — его место), куски лежат в файле в обратном порядке.
        /// </summary>
        public byte[] Uop(int blocksPerChunk, int entriesPerTable, Func<int, int>? nameIndex = null)
        {
            var builder = new UopFileBuilder(5, entriesPerTable);
            var chunkLength = blocksPerChunk * BlockSize;
            var chunks = (Mul.Length + chunkLength - 1) / chunkLength;
            builder.AddEmptySlot();
            builder.Add("build/somethingelse/00000000.dat", new byte[] { 1, 2, 3 });
            for (var chunk = chunks - 1; chunk >= 0; chunk--)
            {
                var length = Math.Min(chunkLength, Mul.Length - chunk * chunkLength);
                var name = "build/map" + Map.FileIndex + "legacymul/" + (nameIndex?.Invoke(chunk) ?? chunk).ToString("D8") + ".dat";
                builder.Add(name, Mul.AsSpan(chunk * chunkLength, length).ToArray());
            }

            return builder.Build();
        }

        private int Cell(int x, int y)
        {
            var block = (x >> 3) * Map.BlockHeight + (y >> 3);
            return block * BlockSize + 4 + (((y & 7) << 3) + (x & 7)) * 3;
        }
    }
}
