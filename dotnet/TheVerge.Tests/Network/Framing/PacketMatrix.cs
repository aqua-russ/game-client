using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TheVerge.Network.Framing;

namespace TheVerge.Tests.Network.Framing
{
    /// <summary>
    /// Чтение docs/protocol/packets.csv для тестов таблицы пакетов. Свой разбор, независимый от генератора
    /// tools/gen: ошибка генератора не повторится здесь. Файл копируется в папку тестов (TheVerge.Tests.csproj).
    /// Формат (поля без запятых и кавычек, 12 столбцов) — docs/protocol/packets.md.
    /// </summary>
    internal static class PacketMatrix
    {
        private const string Header = "id,sub,dir,name,length,condition,server,priority,stage,status,source,note";
        private static readonly Lazy<IReadOnlyList<PacketMatrixRow>> LazyRows = new Lazy<IReadOnlyList<PacketMatrixRow>>(Load);

        public static IReadOnlyList<PacketMatrixRow> Rows => LazyRows.Value;

        private static IReadOnlyList<PacketMatrixRow> Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Network", "Framing", "packets.csv");
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0] != Header)
            {
                throw new InvalidDataException("packets.csv: неожиданный заголовок");
            }

            var rows = new List<PacketMatrixRow>();
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length > 0)
                {
                    rows.Add(Parse(lines[i], i + 1));
                }
            }

            return rows;
        }

        private static PacketMatrixRow Parse(string line, int number)
        {
            var fields = line.Split(',');
            if (fields.Length != 12)
            {
                throw new InvalidDataException("packets.csv:" + number + ": полей " + fields.Length + ", нужно 12");
            }

            var subCommand = fields[1].Length == 0 ? (ushort?)null : ParseHex(fields[1], number);
            var direction = fields[2] switch
            {
                "c2s" => PacketDirection.ClientToServer,
                "s2c" => PacketDirection.ServerToClient,
                _ => throw new InvalidDataException("packets.csv:" + number + ": направление " + fields[2]),
            };
            int? fixedLength = fields[4] == "var" ? (int?)null : int.Parse(fields[4], CultureInfo.InvariantCulture);
            return new PacketMatrixRow(number, (byte)ParseHex(fields[0], number), subCommand, direction, fields[3],
                fixedLength);
        }

        private static ushort ParseHex(string text, int number)
        {
            if (!text.StartsWith("0x", StringComparison.Ordinal) || text.Length != 4)
            {
                throw new InvalidDataException("packets.csv:" + number + ": номер " + text);
            }

            return ushort.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }
}
