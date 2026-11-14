using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Transport;

namespace TheVerge.Bot
{
    /// <summary>
    /// Запись посредника (tools/golden/proxy.py, PLAN Стадия 0, п. 6) → лог пакетов в нашем формате (<see cref="PacketLogWriter"/>):
    /// так эталонный сеанс ClassicUO сравнивается с логами бота. Строка записи: «мс, соединение, событие[, данные]»,
    /// событие — open, c2s / s2c (сырые байты hex), close (c — закрыл клиент, s — сервер). Пароль в логе — звёздочками.
    /// </summary>
    public static class CaptureConverter
    {
        public const string FormatLine = "#TheVerge capture v1";

        public static CaptureResult Convert(TextReader capture, TextWriter log, string source)
        {
            var result = new CaptureResult();
            if (capture.ReadLine() != FormatLine)
            {
                result.AddError("«" + source + "» — не запись посредника: первая строка должна быть «" + FormatLine + "».");
                return result;
            }

            var clock = new CaptureClock();
            var writer = new PacketLogWriter(log, clock, new ResultLog(result), ClientLimits.PacketLogMaxChars);
            writer.Header("source", source);
            var connections = new Dictionary<int, CaptureConnection>();
            var number = 1;
            string? line;
            while ((line = capture.ReadLine()) != null)
            {
                number++;
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                if (!Apply(line, clock, writer, connections, result))
                {
                    result.AddError("строка " + number + ": не разобрать «" + line + "».");
                }
            }

            writer.Flush();
            return result;
        }

        private static bool Apply(string line, CaptureClock clock, PacketLogWriter writer, Dictionary<int, CaptureConnection> connections, CaptureResult result)
        {
            var fields = line.Split('\t');
            if (fields.Length < 3 || !long.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ms) ||
                !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                return false;
            }

            clock.NowMs = ms;
            var data = fields.Length > 3 ? fields[3] : string.Empty;
            switch (fields[2])
            {
                case "open":
                    connections[id] = new CaptureConnection(id, writer, result);
                    result.Connections++;
                    writer.Connected();
                    return true;
                case "close":
                    writer.Closed(data == "s" ? ConnectionCloseReason.ServerClosed : ConnectionCloseReason.Cancelled,
                        data == "s" ? "сервер закрыл соединение " + id : "клиент закрыл соединение " + id);
                    return connections.Remove(id);
                case "c2s":
                case "s2c":
                    return Bytes(fields[2] == "c2s", data, connections.TryGetValue(id, out var connection) ? connection : null);
                default:
                    return false;
            }
        }

        private static bool Bytes(bool fromClient, string hex, CaptureConnection? connection)
        {
            var bytes = FromHex(hex);
            if (connection == null || bytes == null)
            {
                return false;
            }

            if (fromClient)
            {
                connection.FromClient(bytes);
            }
            else
            {
                connection.FromServer(bytes);
            }

            return true;
        }

        private static byte[]? FromHex(string hex)
        {
            if (hex.Length == 0 || hex.Length % 2 != 0)
            {
                return null;
            }

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i]))
                {
                    return null;
                }
            }

            return bytes;
        }
    }
}
