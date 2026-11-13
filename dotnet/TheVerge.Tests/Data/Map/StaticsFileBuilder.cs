using System;
using System.Collections.Generic;
using System.IO;
using TheVerge.Data.Map;

namespace TheVerge.Tests.Data.Map
{
    /// <summary>
    /// Статика в раскладке клиента UO, как её читает сервер (TileMatrix.ReadStaticBlock): staidx — на каждый блок
    /// (x × BlockHeight + y) 12 байт: адрес в statics (int32, −1 — пусто), длина (int32), ещё 4 байта; statics — записи
    /// по 7 байт: номер (ushort), X, Y (байты; клетку сервер берёт по младшим 3 битам), Z (sbyte), цвет (short).
    /// Блоки данных пишутся в перемешанном порядке; часть блоков пустые (адрес −1 или длина 0).
    /// </summary>
    internal sealed class StaticsFileBuilder
    {
        public const int RecordSize = 7;

        public StaticsFileBuilder(MapDefinition map, int seed)
        {
            Map = map;
            var random = new Random(seed);
            Blocks = new List<byte[]>[(int)map.BlockCount];
            for (var block = 0; block < Blocks.Length; block++)
            {
                Blocks[block] = new List<byte[]>();
                var count = random.Next(4) == 0 ? 0 : random.Next(1, 40);
                for (var i = 0; i < count; i++)
                {
                    var record = new byte[RecordSize];
                    random.NextBytes(record);
                    Blocks[block].Add(record);
                }
            }
        }

        public MapDefinition Map { get; }

        // Порядок блоков в файле: i × 11 по модулю числа блоков (перестановка, если число блоков не делится на 11).
        private int BlockAt(int i) => (int)((long)i * 11 % Blocks.Length);

        /// <summary>Записи статики каждого блока по порядку файла.</summary>
        public List<byte[]>[] Blocks { get; }

        /// <summary>Записи клетки по порядку файла, как их отдаёт сервер.</summary>
        public List<byte[]> Cell(int x, int y)
        {
            var result = new List<byte[]>();
            foreach (var record in Blocks[(x >> 3) * Map.BlockHeight + (y >> 3)])
            {
                if ((record[2] & 7) == (x & 7) && (record[3] & 7) == (y & 7))
                {
                    result.Add(record);
                }
            }

            return result;
        }

        /// <summary>Блок, который лежит в statics последним.</summary>
        public int LastBlockInFile => BlockAt(Blocks.Length - 1);

        /// <summary>Добавить блоку записей, чтобы их было не меньше <paramref name="count"/>.</summary>
        public void EnsureRecords(int block, int count)
        {
            while (Blocks[block].Count < count)
            {
                Blocks[block].Add(new byte[] { (byte)Blocks[block].Count, 0x10, 0, 0, 5, 0, 0 });
            }
        }

        public void Build(out byte[] index, out byte[] data)
        {
            index = new byte[Blocks.Length * 12];
            using (var stream = new MemoryStream())
            {
                stream.Write(new byte[5], 0, 5); // данные начинаются не с нуля
                for (var i = 0; i < Blocks.Length; i++)
                {
                    var block = BlockAt(i);
                    var lookup = Blocks[block].Count == 0 ? (block % 2 == 0 ? -1 : (int)stream.Position) : (int)stream.Position;
                    foreach (var record in Blocks[block])
                    {
                        stream.Write(record, 0, record.Length);
                    }

                    BitConverter.GetBytes(lookup).CopyTo(index, block * 12);
                    BitConverter.GetBytes(Blocks[block].Count * RecordSize).CopyTo(index, block * 12 + 4);
                    BitConverter.GetBytes(0x7EADBEEF).CopyTo(index, block * 12 + 8);
                }

                data = stream.ToArray();
            }
        }
    }
}
