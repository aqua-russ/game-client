using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using TheVerge.Core.Multis;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;

namespace TheVerge.Data.Multis
{
    /// <summary>
    /// MultiCollection.uop, как у сервера (MultiData.LoadUOP): куски «NNNNNN.bin», сжатые zlib; в куске — 4 байта, число
    /// частей, часть — номер, x, y, z (по 2 байта), код флагов (2 байта: 1 — None, 257 — Generic, иначе Background),
    /// число clilocs (4) и clilocs по 4 байта (пропускаются). Отличие: несжатый кусок сервер из файла не читает (берёт
    /// старое содержимое буфера) — здесь он читается как есть; в настоящих файлах все куски сжаты.
    /// </summary>
    public static class MultiUopReader
    {
        private const int PartSize = 14;

        public static Dictionary<int, MultiComponents>? Read(string path, out string error)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var entries = UopIndexReader.Read(stream, path, UopNames.Multi, out error);
                if (entries == null)
                {
                    return null;
                }

                var multis = new Dictionary<int, MultiComponents>(entries.Count);
                foreach (var pair in entries)
                {
                    var parts = ReadEntry(stream, pair.Value, out error);
                    if (parts == null)
                    {
                        error = Path.GetFileName(path) + " повреждён: дом или корабль № " + pair.Key + " — " + error + ".";
                        return null;
                    }

                    multis[pair.Key] = MultiComponents.From(parts);
                }

                return multis;
            }
        }

        private static MultiComponent[]? ReadEntry(Stream stream, UopEntry entry, out string error)
        {
            var stored = new byte[entry.Compressed ? entry.CompressedSize : entry.Size];
            stream.Position = entry.Offset;
            if (StreamRead.Fully(stream, stored) < stored.Length)
            {
                error = "кусок за концом файла";
                return null;
            }

            error = string.Empty;
            var data = entry.Compressed ? Zlib.Inflate(stored, entry.Size, out error) : stored;
            return data == null ? null : Parse(data, out error);
        }

        private static MultiComponent[]? Parse(byte[] data, out string error)
        {
            error = "частей больше, чем помещается в кусок";
            var count = data.Length >= 8 ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)) : uint.MaxValue;
            if (count > (uint)(data.Length - 8) / PartSize)
            {
                return null;
            }

            var parts = new MultiComponent[count];
            var at = 8;
            for (var i = 0; i < parts.Length; i++)
            {
                // Число частей выше проверено без clilocs; clilocs прежних частей могли съесть место этой.
                if (data.Length - at < PartSize)
                {
                    error = "части выходят за конец куска";
                    return null;
                }

                var span = data.AsSpan(at);
                var clilocs = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(10));
                if (clilocs > (uint)(data.Length - at - PartSize) / 4)
                {
                    error = "список clilocs длиннее куска";
                    return null;
                }

                parts[i] = new MultiComponent(BinaryPrimitives.ReadUInt16LittleEndian(span), BinaryPrimitives.ReadInt16LittleEndian(span.Slice(2)),
                    BinaryPrimitives.ReadInt16LittleEndian(span.Slice(4)), BinaryPrimitives.ReadInt16LittleEndian(span.Slice(6)),
                    FlagsOf(BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(8))));
                at += PartSize + (int)clilocs * 4;
            }

            error = string.Empty;
            return parts;
        }

        private static TileFlags FlagsOf(ushort code) => code == 1 ? TileFlags.None : code == 257 ? TileFlags.Generic : TileFlags.Background;
    }
}
