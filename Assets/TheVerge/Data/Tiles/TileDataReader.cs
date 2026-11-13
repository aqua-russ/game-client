using System;
using System.Buffers.Binary;
using System.IO;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;

namespace TheVerge.Data.Tiles
{
    /// <summary>
    /// Чтение tiledata.mul (PLAN Стадия 2, п. 2) поле в поле, как сервер (TileData.cs, Load): земля — флаги, 2 байта
    /// текстуры (пропуск), имя; предмет — флаги, вес, качество, анимация, байт, количество, 4 байта, байт, значение,
    /// высота, имя. Имя — ASCII до первого нуля, байты больше 0x7F — «?» (Encoding.ASCII сервера).
    /// </summary>
    public static class TileDataReader
    {
        /// <summary>Таблица, как её видит сервер после запуска: файл (<see cref="ReadFile"/>) и правки сервера.</summary>
        public static TileDataTable? Load(TileDataFormat format, out string error)
        {
            var table = ReadFile(format, out error);
            if (table != null)
            {
                ServerTileDataFixes.Apply(table);
            }

            return table;
        }

        /// <summary>Только файл — как TileData.Load сервера, без правок, которые сервер делает потом.</summary>
        public static TileDataTable? ReadFile(TileDataFormat format, out string error)
        {
            byte[] data;
            try
            {
                data = File.ReadAllBytes(format.Path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = Path.GetFileName(format.Path) + " не читается: " + e.Message;
                return null;
            }

            if (data.Length < format.RequiredLength)
            {
                error = Path.GetFileName(format.Path) + " короче, чем нужно: " + data.Length + " байт, нужно не меньше " +
                        format.RequiredLength + " (файл изменился после проверки папки?).";
                return null;
            }

            error = string.Empty;
            var items = ReadItems(data, format, (int)format.LandSectionLength);
            return new TileDataTable(ReadLand(data, format), items, format.ItemCount);
        }

        private static LandTileData[] ReadLand(byte[] data, TileDataFormat format)
        {
            var land = new LandTileData[TileDataTable.LandTableSize];
            var offset = 0;
            for (var i = 0; i < TileDataFormat.LandCount; i++)
            {
                if (LandHeaderBefore(i, format.Is64BitFlags))
                {
                    offset += TileDataFormat.GroupHeaderSize;
                }

                var flags = ReadFlags(data, ref offset, format.Is64BitFlags);
                offset += 2;
                land[i] = new LandTileData(ReadName(data, ref offset), flags);
            }

            return land;
        }

        private static ItemTileData[] ReadItems(byte[] data, TileDataFormat format, int offset)
        {
            var items = new ItemTileData[TileDataTable.ItemTableSize];
            for (var i = 0; i < format.ItemCount; i++)
            {
                if ((i & (TileDataFormat.GroupSize - 1)) == 0)
                {
                    offset += TileDataFormat.GroupHeaderSize;
                }

                var flags = ReadFlags(data, ref offset, format.Is64BitFlags);
                var properties = new ItemTileProperties(
                    data[offset], data[offset + 1], BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2)),
                    data[offset + 5], data[offset + 11], data[offset + 12]);
                offset += 13;
                items[i] = new ItemTileData(ReadName(data, ref offset), flags, properties);
            }

            var empty = new ItemTileData(string.Empty, TileFlags.None, default);
            for (var i = format.ItemCount; i < items.Length; i++)
            {
                items[i] = empty;
            }

            return items;
        }

        // TileData.cs: в новом формате сервер читает заголовок перед записью №1 и затем перед каждой 32-й, а перед №0 —
        // нет, поэтому №0 у него сдвинута на 4 байта. Повторяем: проходимость клиента должна совпадать с серверной.
        private static bool LandHeaderBefore(int index, bool is64BitFlags) => is64BitFlags
            ? index == 1 || (index > 0 && (index & (TileDataFormat.GroupSize - 1)) == 0)
            : (index & (TileDataFormat.GroupSize - 1)) == 0;

        private static TileFlags ReadFlags(byte[] data, ref int offset, bool is64BitFlags)
        {
            var span = data.AsSpan(offset);
            offset += is64BitFlags ? 8 : 4;
            return (TileFlags)(is64BitFlags ? BinaryPrimitives.ReadUInt64LittleEndian(span) : BinaryPrimitives.ReadUInt32LittleEndian(span));
        }

        private static string ReadName(byte[] data, ref int offset)
        {
            Span<char> name = stackalloc char[TileDataFormat.NameSize];
            var length = 0;
            while (length < TileDataFormat.NameSize && data[offset + length] != 0)
            {
                var b = data[offset + length];
                name[length++] = b < 0x80 ? (char)b : '?';
            }

            offset += TileDataFormat.NameSize;
            return length == 0 ? string.Empty : new string(name.Slice(0, length));
        }
    }
}
