using System;
using System.Globalization;
using System.IO;
using System.Text;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;

namespace TheVerge.Bot
{
    /// <summary>Лог пакетов бота в файл Logs/packets/&lt;время запуска&gt;.tsv; старые файлы сверх 20 удаляются.</summary>
    public sealed class BotPacketLog : IDisposable
    {
        // Буфер записи: лог сбрасывается в файл в конце тика, между сбросами копится в памяти.
        private const int BufferSize = 64 * 1024;

        private readonly StreamWriter _file;

        private BotPacketLog(string path, StreamWriter file, PacketLogWriter writer)
        {
            Path = path;
            _file = file;
            Writer = writer;
        }

        public string Path { get; }

        public PacketLogWriter Writer { get; }

        public static BotPacketLog Open(string clientRoot, BotSettings settings, IClock clock, ILog log)
        {
            var started = DateTimeOffset.Now;
            var directory = BotPaths.PacketLogs(clientRoot);
            var path = PacketLogFiles.NewPath(directory, started.LocalDateTime);
            var file = new StreamWriter(path, false, new UTF8Encoding(false), BufferSize);
            PacketLogFiles.Prune(directory);
            var writer = new PacketLogWriter(file, clock, log, ClientLimits.PacketLogMaxChars);
            writer.Header("started", started.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture));
            writer.Header("client", "TheVerge.Bot");
            writer.Header("server", settings.Host + ":" + settings.Port);
            return new BotPacketLog(path, file, writer);
        }

        public void Dispose()
        {
            Writer.Flush();
            _file.Dispose();
        }
    }
}
