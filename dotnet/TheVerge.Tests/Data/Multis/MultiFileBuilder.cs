using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TheVerge.Tests.Data.Files;

namespace TheVerge.Tests.Data.Multis
{
    /// <summary>
    /// Дома и корабли в раскладке клиента UO, как их читает сервер (MultiData.cs). MUL: multi.idx — на номер 12 байт
    /// (адрес, длина, ещё 4), multi.mul — части по 16 байт (номер, x, y, z по 2 байта, флаги 8) или по 12 (флаги 4).
    /// UOP (MultiCollection.uop, версия 4, куски «build/multicollection/NNNNNN.bin», сжаты zlib): 4 байта, число частей,
    /// часть — номер, x, y, z (по 2 байта), код флагов (2: 0 — Background, 1 — None, 257 — Generic), число clilocs (4)
    /// и clilocs по 4 байта.
    /// </summary>
    internal sealed class MultiFileBuilder
    {
        public Dictionary<int, List<Part>> Multis { get; } = new Dictionary<int, List<Part>>();

        public void Add(int id, params Part[] parts) => Multis[id] = new List<Part>(parts);

        public void BuildMul(bool postHs, int count, out byte[] index, out byte[] data)
        {
            index = new byte[count * 12];
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new byte[3]);
                for (var id = 0; id < count; id++)
                {
                    var lookup = -1;
                    var length = 0;
                    if (Multis.TryGetValue(id, out var parts))
                    {
                        lookup = (int)stream.Position;
                        WriteMulParts(writer, parts, postHs);
                        length = (int)stream.Position - lookup;
                    }

                    BitConverter.GetBytes(lookup).CopyTo(index, id * 12);
                    BitConverter.GetBytes(length).CopyTo(index, id * 12 + 4);
                }

                writer.Flush();
                data = stream.ToArray();
            }
        }

        private static void WriteMulParts(BinaryWriter writer, List<Part> parts, bool postHs)
        {
            foreach (var part in parts)
            {
                WriteCoordinates(writer, part);
                if (postHs)
                {
                    writer.Write(part.Flags);
                }
                else
                {
                    writer.Write((uint)part.Flags);
                }
            }
        }

        public byte[] BuildUop(bool compress = true)
        {
            var builder = new UopFileBuilder(4, 7);
            builder.Add("build/multicollection/housing.bin", new byte[] { 9, 9, 9 });
            foreach (var pair in Multis)
            {
                var name = "build/multicollection/" + pair.Key.ToString("D6") + ".bin";
                var raw = UopEntry(pair.Value);
                if (compress)
                {
                    builder.AddCompressed(name, Zlib(raw), raw.Length);
                }
                else
                {
                    builder.Add(name, raw);
                }
            }

            return builder.Build();
        }

        public static byte[] UopEntry(List<Part> parts)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x0BADF00D);
                writer.Write((uint)parts.Count);
                foreach (var part in parts)
                {
                    WriteCoordinates(writer, part);
                    writer.Write(part.UopFlagCode);
                    writer.Write((uint)part.Clilocs);
                    for (var i = 0; i < part.Clilocs; i++)
                    {
                        writer.Write(1000000 + i);
                    }
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static byte[] Zlib(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
                {
                    zlib.Write(raw, 0, raw.Length);
                }

                return output.ToArray();
            }
        }

        private static void WriteCoordinates(BinaryWriter writer, Part part)
        {
            writer.Write(part.ItemId);
            writer.Write(part.X);
            writer.Write(part.Y);
            writer.Write(part.Z);
        }

        internal readonly struct Part
        {
            public Part(ushort itemId, short x, short y, short z, ulong flags, ushort uopFlagCode = 0, int clilocs = 0)
            {
                ItemId = itemId;
                X = x;
                Y = y;
                Z = z;
                Flags = flags;
                UopFlagCode = uopFlagCode;
                Clilocs = clilocs;
            }

            public ushort ItemId { get; }
            public short X { get; }
            public short Y { get; }
            public short Z { get; }
            public ulong Flags { get; }
            public ushort UopFlagCode { get; }
            public int Clilocs { get; }
        }
    }
}
