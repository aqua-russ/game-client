using System;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.World;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Packets.Items;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bot
{
    /// <summary>
    /// Клиент бота целиком — то же, что собирает Bootstrap в Unity: сеть (NetworkSession, обработчики пакетов), вход
    /// (<see cref="LoginFlow"/>), вход в мир (<see cref="WorldEntry"/>), мир (<see cref="EntityStore"/>), игровая сессия (<see cref="GameSession"/>) и ядро
    /// (<see cref="GameClient"/>).
    /// </summary>
    internal sealed class BotClient : IDisposable
    {
        public BotClient(Func<IConnection> newConnection, IClock clock, ILog log, PacketLogWriter? packetLog,
            System.Threading.CancellationToken stop, BotLogin login)
        {
            var dispatcher = new PacketDispatcher(log);
            Session = new NetworkSession(dispatcher, log) { PacketLog = packetLog };
            var connector = new ServerConnector(Session, newConnection, stop);
            Flow = new LoginFlow(new AccountSender(Session), connector, NewSeed());
            var sessionSender = new SessionSender(Session);
            World = new WorldEntry(Flow.Progress, sessionSender);
            Game = new GameSession(Flow.Progress, sessionSender, connector, clock);
            AccountHandlers.Register(dispatcher, Flow);
            SessionHandlers.Register(dispatcher, World, new ServerRequests(sessionSender), Game);
            var entities = new EntityStore();
            var containers = new ContainerContents(entities);
            InWorld = new BotWorld(entities, new WorldEnvironment(), containers, new ItemCommands(entities, new ItemSender(Session), clock));
            WorldHandlers.Register(dispatcher, entities, InWorld.Environment);
            ItemHandlers.Register(dispatcher, containers);
            World.PlayerConfirmed += Entities.Reset;
            Session.Observer = Flow;
            var watchdog = new LoginWatchdog(Flow.Progress);
            Reconnector = login.Reconnect ? new Reconnector(Flow) : null;
            Core = Reconnector == null
                ? new GameClient(Session, clock, Game, watchdog)
                : new GameClient(Session, clock, Game, watchdog, Reconnector);
        }

        public NetworkSession Session { get; }

        public LoginFlow Flow { get; }

        public WorldEntry World { get; }

        public GameSession Game { get; }

        /// <summary>Мир (PLAN Стадия 2): объекты, окружение, контейнеры и действия с предметами.</summary>
        public BotWorld InWorld { get; }

        /// <summary>Мобы и предметы, которые прислал сервер.</summary>
        public EntityStore Entities => InWorld.Entities;

        public GameClient Core { get; }

        /// <summary>Повтор входа; null — без --reconnect.</summary>
        public Reconnector? Reconnector { get; }

        /// <summary>Вход закончился и повтора не будет.</summary>
        public bool IsFinished => Flow.IsFinished && (Reconnector == null || !Reconnector.WillRetry);

        // Seed логин-сервера: любое число, кроме 0 (NetState.cs: нулевой сервер отключает).
        private static int NewSeed() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);

        public void Dispose()
        {
            World.PlayerConfirmed -= Entities.Reset;
            Session.Dispose();
        }
    }
}
