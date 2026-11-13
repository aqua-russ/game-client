using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace TheVerge.Data.Files
{
    /// <summary>
    /// Таблицы кусков UOP, как их читает сервер (UOPFiles.ReadUOPIndexes): по цепочке таблиц, записи по 34 байта; кусок
    /// с адресом 0 или нулевой длиной пропускается; номер куска — по хешу имени «build/&lt;имя файла&gt;/&lt;номер&gt;&lt;расширение&gt;»
    /// (номер — <c>precision</c> цифр), чужие имена пропускаются. Сломанный файл (таблица за концом файла, таблицы по
    /// кругу) — ошибка словами: сервер на таком упал бы или завис.
    /// </summary>
    public static class UopIndexReader
    {
        private const int TableHeaderSize = 12;
        private const int EntrySize = 34;

        public static Dictionary<int, UopEntry>? Read(Stream stream, string fileName, UopNames names, out string error)
        {
            var hashes = Hashes(fileName, names);
            var entries = new Dictionary<int, UopEntry>();
            var header = new byte[UopHeader.Length];
            stream.Position = 0;
            error = StreamRead.Fully(stream, header) < UopHeader.Length ? "короче заголовка" : string.Empty;
            var next = error.Length == 0 ? BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(12)) : 0;
            var visited = new HashSet<long>();
            var length = stream.Length;
            while (error.Length == 0)
            {
                if (next < 0 || next + TableHeaderSize > length || !visited.Add(next))
                {
                    error = next >= 0 && next < length && visited.Contains(next) ? "таблицы кусков идут по кругу" : "таблица кусков за концом файла";
                    break;
                }

                next = ReadTable(stream, next, hashes, entries, out error);
                if (next == 0 || next >= length)
                {
                    break;
                }
            }

            if (error.Length > 0)
            {
                error = Path.GetFileName(fileName) + " повреждён: " + error + ".";
                return null;
            }

            return entries;
        }

        private static long ReadTable(Stream stream, long position, Dictionary<ulong, int> hashes, Dictionary<int, UopEntry> entries, out string error)
        {
            var head = new byte[TableHeaderSize];
            stream.Position = position;
            StreamRead.Fully(stream, head);
            var count = BinaryPrimitives.ReadInt32LittleEndian(head);
            var next = BinaryPrimitives.ReadInt64LittleEndian(head.AsSpan(4));
            if (count < 0 || position + TableHeaderSize + (long)count * EntrySize > stream.Length)
            {
                error = "таблица кусков длиннее файла";
                return 0;
            }

            var table = new byte[count * EntrySize];
            StreamRead.Fully(stream, table);
            for (var i = 0; i < count; i++)
            {
                AddEntry(table.AsSpan(i * EntrySize, EntrySize), hashes, entries);
            }

            error = string.Empty;
            return next;
        }

        private static void AddEntry(ReadOnlySpan<byte> record, Dictionary<ulong, int> hashes, Dictionary<int, UopEntry> entries)
        {
            var offset = BinaryPrimitives.ReadInt64LittleEndian(record);
            var headerLength = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(8));
            var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(12));
            var length = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(16));
            var hash = BinaryPrimitives.ReadUInt64LittleEndian(record.Slice(20));
            var compressed = BinaryPrimitives.ReadInt16LittleEndian(record.Slice(32)) == 1;
            if (offset != 0 && compressedLength > 0 && length > 0 && hashes.TryGetValue(hash, out var index))
            {
                entries[index] = new UopEntry(offset + headerLength, length, compressedLength, compressed);
            }
        }

        private static Dictionary<ulong, int> Hashes(string fileName, UopNames names)
        {
            var root = "build/" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant() + "/";
            var format = "D" + names.Digits;
            var hashes = new Dictionary<ulong, int>(names.Count);
            for (var i = 0; i < names.Count; i++)
            {
                hashes[UopHash.HashLittle2(root + i.ToString(format) + names.Extension)] = i;
            }

            return hashes;
        }
    }
}
