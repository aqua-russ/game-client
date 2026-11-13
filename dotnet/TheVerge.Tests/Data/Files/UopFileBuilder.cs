using System.Collections.Generic;
using System.IO;
using TheVerge.Data.Files;

namespace TheVerge.Tests.Data.Files
{
    /// <summary>
    /// Собирает файл UOP так, как его читает сервер (UOPFiles.ReadUOPIndexes): заголовок «MYP\0», версия, подпись
    /// 0xFD23EC43, адрес первой таблицы; таблица — число записей, адрес следующей таблицы (0 — последняя) и записи по
    /// 34 байта: адрес заголовка куска, длина заголовка, длина сжатая и полная, хеш имени (HashLittle2), Adler32,
    /// признак сжатия. Данные куска начинаются после его заголовка. Куски пишутся в заданном порядке, таблицы — в конце.
    /// </summary>
    internal sealed class UopFileBuilder
    {
        private const int ChunkHeaderLength = 8;
        private readonly List<(ulong Hash, byte[] Data, int Length, bool Compressed, bool Skipped)> _entries =
            new List<(ulong, byte[], int, bool, bool)>();

        public UopFileBuilder(int version, int entriesPerTable)
        {
            Version = version;
            EntriesPerTable = entriesPerTable;
        }

        public int Version { get; }
        public int EntriesPerTable { get; }

        public void Add(string name, byte[] data) => _entries.Add((UopHash.HashLittle2(name), data, data.Length, false, false));

        /// <summary>Сжатый кусок: в файле — <paramref name="stored"/>, полная длина — <paramref name="length"/>.</summary>
        public void AddCompressed(string name, byte[] stored, int length) =>
            _entries.Add((UopHash.HashLittle2(name), stored, length, true, false));

        /// <summary>Запись с адресом 0 — сервер её пропускает.</summary>
        public void AddEmptySlot() => _entries.Add((0, new byte[0], 0, false, true));

        public byte[] Build()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(UopHeader.Magic);
                writer.Write(Version);
                writer.Write(UopHeader.Signature);
                var firstTablePosition = stream.Position;
                writer.Write(0L);
                writer.Write(EntriesPerTable);
                writer.Write(_entries.Count);
                var offsets = new long[_entries.Count];
                for (var i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Skipped)
                    {
                        continue;
                    }

                    offsets[i] = stream.Position;
                    writer.Write(0x1234567812345678L);
                    writer.Write(_entries[i].Data);
                }

                WriteTables(writer, offsets, firstTablePosition);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private void WriteTables(BinaryWriter writer, long[] offsets, long pointerPosition)
        {
            var stream = writer.BaseStream;
            for (var start = 0; start < _entries.Count; start += EntriesPerTable)
            {
                var tablePosition = stream.Position;
                stream.Position = pointerPosition;
                writer.Write(tablePosition);
                stream.Position = tablePosition;
                var count = System.Math.Min(EntriesPerTable, _entries.Count - start);
                writer.Write(count);
                pointerPosition = stream.Position;
                writer.Write(0L);
                for (var i = start; i < start + count; i++)
                {
                    var entry = _entries[i];
                    writer.Write(offsets[i]);
                    writer.Write(entry.Skipped ? 0 : ChunkHeaderLength);
                    writer.Write(entry.Data.Length);
                    writer.Write(entry.Length);
                    writer.Write(entry.Hash);
                    writer.Write(0u);
                    writer.Write((short)(entry.Compressed ? 1 : 0));
                }
            }
        }
    }
}
