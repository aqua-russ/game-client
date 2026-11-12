using System;
using TheVerge.Network.Framing;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Формат лога пакетов, версия 1 (PLAN Стадия 1, п. 12). Текст UTF-8, строки через «\n», поля через табуляцию.
    /// Первая строка — версия формата; строки с «#» — заголовки и пометки. Пакет:
    /// «время  S>C|C>S  0xID  подкоманда|-  имя  длина  пометки|-  байты». События: «время  CONNECTED»,
    /// «время  CLOSED  причина  подробности». Время — мс от начала лога. Имя — справочное (из PacketTable);
    /// при чтении проверяется всё остальное, а байты — источник правды.
    /// </summary>
    internal static class PacketLogFormat
    {
        public const string VersionLine = "#TheVerge packet log v1";
        public const char Comment = '#';
        public const char Separator = '\t';
        public const string Truncated = "truncated";
        public const string ServerToClient = "S>C";
        public const string ClientToServer = "C>S";
        public const string Connected = "CONNECTED";
        public const string Closed = "CLOSED";
        public const string Empty = "-";
        public const string UnknownName = "?";
        public const string Masked = "masked";
        public const string Unsent = "unsent";
        public const char FlagSeparator = ',';
        public const int PacketFields = 8;
        public const int ConnectedFields = 2;
        public const int ClosedFields = 4;

        public static string DirectionText(PacketDirection direction) =>
            direction == PacketDirection.ServerToClient ? ServerToClient : ClientToServer;

        /// <summary>Подкоманда пакета, если она у него есть и пакет до неё дотягивается; size — 1 или 2 байта.</summary>
        public static bool TryReadSubCommand(PacketDirection direction, ReadOnlySpan<byte> packet, out ushort subCommand, out int size)
        {
            var layout = PacketTable.GetSubCommandLayout(direction, packet[0]);
            if (!layout.HasSubCommand || packet.Length < layout.MinPacketLength)
            {
                subCommand = 0;
                size = 0;
                return false;
            }

            subCommand = layout.Read(packet);
            size = layout.Size;
            return true;
        }

        public static string Name(PacketDirection direction, ReadOnlySpan<byte> packet)
        {
            var info = TryReadSubCommand(direction, packet, out var subCommand, out _)
                ? PacketTable.Get(direction, packet[0], subCommand)
                : PacketTable.Get(direction, packet[0]);
            return info.IsKnown ? info.Name : UnknownName;
        }

        /// <summary>Байты из «73 07 …»: пары цифр 0–9, A–F через один пробел. false — строка не в этом виде.</summary>
        public static bool TryParseBytes(string text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (text.Length == 0 || (text.Length + 1) % 3 != 0)
            {
                return false;
            }

            var result = new byte[(text.Length + 1) / 3];
            for (var i = 0; i < result.Length; i++)
            {
                var at = i * 3;
                var high = HexDigit(text[at]);
                var low = HexDigit(text[at + 1]);
                if (high < 0 || low < 0 || (at + 2 < text.Length && text[at + 2] != ' '))
                {
                    return false;
                }

                result[i] = (byte)((high << 4) | low);
            }

            bytes = result;
            return true;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            return c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
        }
    }
}
