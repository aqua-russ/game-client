using System;
using System.IO;
using System.Text;

namespace TheVerge.Tests.Data.Tiles
{
    /// <summary>
    /// Собирает tiledata.mul в раскладке файла клиента UO — той, что читает сервер (TileData.cs, Load): земля 0x4000
    /// записей, затем предметы; перед каждой группой из 32 записей — 4 байта заголовка. Земля: флаги (4 или 8 байт),
    /// текстура (2), имя (20). Предмет: флаги, вес, качество, анимация (2), байт, количество, 4 байта, байт, значение,
    /// высота, имя (20). Числа little-endian. Значения — случайные с заданным seed.
    /// </summary>
    internal sealed class TileDataFileBuilder
    {
        public const int NameLength = 20;

        public TileDataFileBuilder(bool is64BitFlags, int itemCount, int seed)
        {
            Is64BitFlags = is64BitFlags;
            ItemCount = itemCount;
            var random = new Random(seed);
            LandFlags = new ulong[0x4000];
            LandNames = new byte[0x4000][];
            ItemFlags = new ulong[itemCount];
            ItemNames = new byte[itemCount][];
            ItemBytes = new byte[itemCount][];
            for (var i = 0; i < LandFlags.Length; i++)
            {
                LandFlags[i] = Flags(random);
                LandNames[i] = Name(random);
            }

            for (var i = 0; i < itemCount; i++)
            {
                ItemFlags[i] = Flags(random);
                ItemNames[i] = Name(random);
                ItemBytes[i] = new byte[13];
                random.NextBytes(ItemBytes[i]);
            }
        }

        public bool Is64BitFlags { get; }
        public int ItemCount { get; }
        public ulong[] LandFlags { get; }
        public byte[][] LandNames { get; }
        public ulong[] ItemFlags { get; }
        public byte[][] ItemNames { get; }

        /// <summary>13 байт записи предмета между флагами и именем, по порядку файла.</summary>
        public byte[][] ItemBytes { get; }

        public byte[] Build()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                for (var i = 0; i < LandFlags.Length; i++)
                {
                    Header(writer, i);
                    WriteFlags(writer, LandFlags[i]);
                    writer.Write((ushort)(i * 7));
                    writer.Write(LandNames[i]);
                }

                for (var i = 0; i < ItemCount; i++)
                {
                    Header(writer, i);
                    WriteFlags(writer, ItemFlags[i]);
                    writer.Write(ItemBytes[i]);
                    writer.Write(ItemNames[i]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Имя так, как его понимает сервер: ASCII до первого нуля, байты больше 0x7F — «?».</summary>
        public static string Decode(byte[] name)
        {
            var end = Array.IndexOf(name, (byte)0);
            var text = Encoding.ASCII.GetString(name, 0, end < 0 ? name.Length : end);
            return text;
        }

        private static void Header(BinaryWriter writer, int index)
        {
            if ((index & 0x1F) == 0)
            {
                writer.Write(unchecked((int)0xA5A50000) | index);
            }
        }

        private void WriteFlags(BinaryWriter writer, ulong flags)
        {
            if (Is64BitFlags)
            {
                writer.Write(flags);
            }
            else
            {
                writer.Write((uint)flags);
            }
        }

        private ulong Flags(Random random)
        {
            var low = (ulong)(uint)random.Next() | ((ulong)(uint)random.Next(2) << 31);
            return Is64BitFlags ? low | ((ulong)(uint)random.Next() << 32) : low;
        }

        private static byte[] Name(Random random)
        {
            var name = new byte[NameLength];
            var length = random.Next(NameLength + 1);
            for (var i = 0; i < length; i++)
            {
                name[i] = (byte)random.Next(0x20, 0x7F);
            }

            if (length < NameLength - 1)
            {
                name[length + 1] = (byte)'X'; // мусор после нуля сервер не читает
            }

            return name;
        }
    }
}
