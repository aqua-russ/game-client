using System;
using System.Buffers;
using System.Threading;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Клиент на одну попытку входа — то же, что BotClient у бота (dotnet/TheVerge.Bot/BotClient.cs): сеть
    /// (NetworkSession, обработчики пакетов), вход, вход в мир, игровая сессия и ядро. Отличие от бота: повтор входа
    /// (<see cref="Reconnector"/>) в Unity включён всегда (ADR 0016 п. 6).
    /// </summary>
    public static class UnityLoginClient
    {
        private static readonly Random SeedSource = new Random();

        /// <param name="stop">Отмена закрывает соединение (выход из Play, закрытие игры).</param>
        public static LoginParts Create(IClock clock, ILog log, CancellationToken stop)
        {
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(dispatcher, log);
            var connector = new ServerConnector(session, NewConnection, stop);
            var flow = new LoginFlow(new AccountSender(session), connector, NewSeed());
            var sessionSender = new SessionSender(session);
            var world = new WorldEntry(flow.Progress, sessionSender);
            var game = new GameSession(flow.Progress, sessionSender, connector, clock);
            AccountHandlers.Register(dispatcher, flow);
            SessionHandlers.Register(dispatcher, world, new ServerRequests(sessionSender), game);
            session.Observer = flow;
            var reconnector = new Reconnector(flow);
            var core = new GameClient(session, clock, game, new LoginWatchdog(flow.Progress), reconnector);
            return new LoginParts(flow, game, reconnector, core, session);
        }

        private static IConnection NewConnection() =>
            new TcpConnection(ArrayPool<byte>.Shared, ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes);

        // Seed логин-сервера: любое число, кроме 0 (NetState.cs: нулевой сервер отключает).
        private static int NewSeed() => SeedSource.Next(1, int.MaxValue);
    }
}
