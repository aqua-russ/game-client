using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using TheVerge.Core.Multis;
using TheVerge.Core.Tiles;

namespace TheVerge.Data.Multis
{
    /// <summary>
    /// multi.idx + multi.mul, как у сервера (MultiData.LoadMul): номеров — длина индекса / 12; запись индекса — адрес,
    /// длина, ещё 4 байта; адрес меньше 0 или длина 0 — номера нет; частей — длина / размер записи (16 или 12); часть —
    /// номер, x, y, z (по 2 байта), флаги (8 или 4). Номер, данные которого за концом файла, — ошибка (сервер бы упал).
    /// </summary>
    public static class MultiMulReader
    {
        private const int IndexRecordSize = 12;

        public static Dictionary<int, MultiComponents>? Read(string indexPath, string dataPath, int recordSize, out string error)
        {
            var index = File.ReadAllBytes(indexPath);
            var data = File.ReadAllBytes(dataPath);
            var multis = new Dictionary<int, MultiComponents>();
            for (var id = 0; id < index.Length / IndexRecordSize; id++)
            {
                var lookup = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(id * IndexRecordSize));
                var length = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(id * IndexRecordSize + 4));
                if (lookup < 0 || length <= 0)
                {
                    continue;
                }

                var count = length / recordSize;
                if ((long)lookup + (long)count * recordSize > data.Length)
                {
                    error = Path.GetFileName(dataPath) + " повреждён: части дома или корабля № " + id + " за концом файла.";
                    return null;
                }

                multis[id] = MultiComponents.From(Parts(data, lookup, count, recordSize));
            }

            error = string.Empty;
            return multis;
        }

        private static MultiComponent[] Parts(byte[] data, int at, int count, int recordSize)
        {
            var parts = new MultiComponent[count];
            for (var i = 0; i < count; i++, at += recordSize)
            {
                var span = data.AsSpan(at);
                var flags = recordSize == 16 ? BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(8)) : BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8));
                parts[i] = new MultiComponent(BinaryPrimitives.ReadUInt16LittleEndian(span), BinaryPrimitives.ReadInt16LittleEndian(span.Slice(2)),
                    BinaryPrimitives.ReadInt16LittleEndian(span.Slice(4)), BinaryPrimitives.ReadInt16LittleEndian(span.Slice(6)), (TileFlags)flags);
            }

            return parts;
        }
    }
}
