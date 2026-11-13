using System;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bot
{
    /// <summary>
    /// Клиент бота целиком — то же, что собирает Bootstrap в Unity: сеть (NetworkSession, обработчики пакетов), вход
    /// (<see cref="LoginFlow"/>), вход в мир (<see cref="WorldEntry"/>), игровая сессия (<see cref="GameSession"/>) и ядро
    /// (<see cref="GameClient"/>).
    /// </summary>
    internal sealed class BotClient : IDisposable
    {
        public BotClient(Func<IConnection> newConnection, IClock clock, ILog log, PacketLogWriter? packetLog,
            System.Threading.CancellationToken stop, int seed)
        {
            var dispatcher = new PacketDispatcher(log);
            Session = new NetworkSession(dispatcher, log) { PacketLog = packetLog };
            var connector = new ServerConnector(Session, newConnection, stop);
            Flow = new LoginFlow(new AccountSender(Session), connector, seed);
            var sessionSender = new SessionSender(Session);
            World = new WorldEntry(Flow.Progress, sessionSender);
            Game = new GameSession(Flow.Progress, sessionSender, connector, clock);
            AccountHandlers.Register(dispatcher, Flow);
            SessionHandlers.Register(dispatcher, World, new ServerRequests(sessionSender), Game);
            Session.Observer = Flow;
            Core = new GameClient(Session, clock, Game);
        }

        public NetworkSession Session { get; }

        public LoginFlow Flow { get; }

        public WorldEntry World { get; }

        public GameSession Game { get; }

        public GameClient Core { get; }

        public void Dispose() => Session.Dispose();
    }
}
