using System;
using System.Globalization;
using System.IO;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Чтение лога пакетов обратно (формат — <see cref="PacketLogFormat"/>): для фейкового сервера и сверки распаковки
    /// (PLAN Стадия 1, пп. 3, 13). Каждая строка проверяется на согласованность — номер, подкоманда и длина должны
    /// совпадать с байтами; любое расхождение — <see cref="PacketLogFormatException"/> с номером строки, а не тихий пропуск.
    /// </summary>
    public sealed class PacketLogReader
    {
        private static readonly string TruncatedPrefix = PacketLogFormat.Comment + PacketLogFormat.Truncated + PacketLogFormat.Separator;

        private readonly TextReader _input;
        private int _lineNumber;

        public PacketLogReader(TextReader input)
        {
            _input = input;
        }

        /// <summary>В логе встретилась пометка «#truncated»: запись была остановлена по пределу размера, сессия неполная.</summary>
        public bool IsTruncated { get; private set; }

        public bool TryRead(out PacketLogEntry entry)
        {
            string? line;
            while ((line = _input.ReadLine()) != null)
            {
                _lineNumber++;
                if (_lineNumber == 1)
                {
                    RequireVersion(line);
                }
                else if (line.Length > 0 && line[0] == PacketLogFormat.Comment)
                {
                    IsTruncated |= line.StartsWith(TruncatedPrefix, StringComparison.Ordinal);
                }
                else if (line.Length > 0)
                {
                    entry = Parse(line.Split(PacketLogFormat.Separator));
                    return true;
                }
            }

            if (_lineNumber == 0)
            {
                throw new PacketLogFormatException(1, "файл пустой.");
            }

            entry = default;
            return false;
        }

        private void RequireVersion(string line)
        {
            if (line != PacketLogFormat.VersionLine)
            {
                throw Error("первая строка должна быть «" + PacketLogFormat.VersionLine + "»: это не лог пакетов или другая версия формата.");
            }
        }

        private PacketLogEntry Parse(string[] fields)
        {
            var time = ParseTime(fields[0]);
            switch (fields.Length > 1 ? fields[1] : string.Empty)
            {
                case PacketLogFormat.ServerToClient:
                    return ParsePacket(time, PacketDirection.ServerToClient, fields);
                case PacketLogFormat.ClientToServer:
                    return ParsePacket(time, PacketDirection.ClientToServer, fields);
                case PacketLogFormat.Connected:
                    RequireFields(fields, PacketLogFormat.ConnectedFields);
                    return PacketLogEntry.ForConnected(time);
                case PacketLogFormat.Closed:
                    RequireFields(fields, PacketLogFormat.ClosedFields);
                    return PacketLogEntry.ForClosed(time, ParseCloseReason(fields[2]), fields[3]);
                default:
                    throw Error("нет направления пакета (S>C или C>S) или события (CONNECTED, CLOSED).");
            }
        }

        private PacketLogEntry ParsePacket(long time, PacketDirection direction, string[] fields)
        {
            RequireFields(fields, PacketLogFormat.PacketFields);
            var packet = ParseBytes(fields[7]);
            if (fields[2] != HexNumber(packet[0], 1))
            {
                throw Error("номер «" + fields[2] + "» не совпадает с первым байтом пакета.");
            }

            if (fields[3] != SubCommandText(direction, packet))
            {
                throw Error("подкоманда «" + fields[3] + "» не совпадает с байтами пакета.");
            }

            if (fields[5] != packet.Length.ToString(CultureInfo.InvariantCulture))
            {
                throw Error("длина «" + fields[5] + "» не совпадает с числом байт пакета (" + packet.Length + ").");
            }

            ParseFlags(fields[6], out var masked, out var sent);
            return PacketLogEntry.ForPacket(time, direction, packet, masked, sent);
        }

        private long ParseTime(string text)
        {
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var time))
            {
                throw Error("время «" + text + "» — не число миллисекунд.");
            }

            return time;
        }

        private byte[] ParseBytes(string text)
        {
            if (!PacketLogFormat.TryParseBytes(text, out var bytes))
            {
                throw Error("байты пакета должны быть парами шестнадцатеричных цифр (0–9, A–F) через один пробел.");
            }

            return bytes;
        }

        private void ParseFlags(string text, out bool masked, out bool sent)
        {
            masked = false;
            sent = true;
            if (text == PacketLogFormat.Empty)
            {
                return;
            }

            foreach (var flag in text.Split(PacketLogFormat.FlagSeparator))
            {
                if (flag == PacketLogFormat.Masked && !masked)
                {
                    masked = true;
                }
                else if (flag == PacketLogFormat.Unsent && sent)
                {
                    sent = false;
                }
                else
                {
                    throw Error("неизвестная или повторная пометка «" + flag + "».");
                }
            }
        }

        private ConnectionCloseReason ParseCloseReason(string text)
        {
            // Число вместо имени не принимаем: в логе пишется имя причины, и «99» значит, что файл испорчен.
            if (text.Length == 0 || char.IsDigit(text[0]) || !Enum.TryParse(text, false, out ConnectionCloseReason reason) ||
                !Enum.IsDefined(typeof(ConnectionCloseReason), reason))
            {
                throw Error("неизвестная причина закрытия «" + text + "».");
            }

            return reason;
        }

        private void RequireFields(string[] fields, int count)
        {
            if (fields.Length != count)
            {
                throw Error("полей " + fields.Length + ", а должно быть " + count + ".");
            }
        }

        private static string SubCommandText(PacketDirection direction, byte[] packet) =>
            PacketLogFormat.TryReadSubCommand(direction, packet, out var subCommand, out var size)
                ? HexNumber(subCommand, size)
                : PacketLogFormat.Empty;

        private static string HexNumber(int value, int bytes) =>
            "0x" + value.ToString(bytes == 1 ? "X2" : "X4", CultureInfo.InvariantCulture);

        private PacketLogFormatException Error(string problem) => new PacketLogFormatException(_lineNumber, problem);
    }
}
