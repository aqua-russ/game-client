using System;
using System.Buffers;
using System.IO;
using TheVerge.Core.Session;
using TheVerge.Data;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bot
{
    /// <summary>
    /// Самопроверка без сервера (tools/check/all.sh): сборки загружаются, цепочка TcpConnection → NetworkSession →
    /// GameClient с логом пакетов собирается и закрывается. Печатает «TheVerge.Bot: ОК».
    /// </summary>
    public static class BotSelfCheck
    {
        public static int Run(TextWriter output)
        {
            output.WriteLine("TheVerge.Bot: сборки загружены:");
            PrintAssembly(output, typeof(GameClient));
            PrintAssembly(output, typeof(NetworkSession));
            PrintAssembly(output, typeof(DataAssembly));
            var log = new ConsoleLog(TextWriter.Null);
            var clock = new StopwatchClock();
            var connection = new TcpConnection(ArrayPool<byte>.Shared, ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes);
            using (var session = new NetworkSession(connection, new PacketDispatcher(log), log))
            {
                session.PacketLog = new PacketLogWriter(TextWriter.Null, clock, log, ClientLimits.PacketLogMaxChars);
                new GameClient(session, clock).Tick(clock.NowMs);
            }

            output.WriteLine("TheVerge.Bot: ОК (самопроверка без сервера: цепочка сети собирается и закрывается)");
            return BotExitCode.Success;
        }

        private static void PrintAssembly(TextWriter output, Type marker)
        {
            var name = marker.Assembly.GetName();
            output.WriteLine("  " + name.Name + " " + name.Version);
        }
    }
}
