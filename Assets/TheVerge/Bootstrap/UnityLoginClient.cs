using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Клиент на одну попытку входа — то же, что BotClient у бота (dotnet/TheVerge.Bot/BotClient.cs): сеть
    /// (NetworkSession, обработчики пакетов), вход, вход в мир, игровая сессия, мир с движением (<see cref="UnityWorld"/>) и
    /// ядро. Отличия от бота: повтор входа (<see cref="Reconnector"/>) в Unity включён всегда (ADR 0016 п. 6); каждое
    /// соединение обёрнуто симуляцией пинга (<see cref="LaggedConnection"/>; выключена — пакеты идут насквозь).
    /// </summary>
    public static class UnityLoginClient
    {
        private static readonly Random SeedSource = new Random();

        /// <param name="stop">Отмена закрывает соединение (выход из Play, закрытие игры).</param>
        /// <param name="options">Файлы данных (null — мир без ходьбы) и настройки симуляции пинга.</param>
        /// <param name="world">Мир этого входа: мобы и предметы по пакетам сервера, чужие мобы и своё движение.</param>
        public static LoginParts Create(IClock clock, ILog log, CancellationToken stop, UnityClientOptions options, out UnityWorld world)
        {
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(dispatcher, log);
            var lag = options.Lag;
            var connector = new ServerConnector(session, () => NewConnection(clock, lag), stop);
            var flow = new LoginFlow(new AccountSender(session), connector, NewSeed());
            var sessionSender = new SessionSender(session);
            var entry = new WorldEntry(flow.Progress, sessionSender);
            var game = new GameSession(flow.Progress, sessionSender, connector, clock);
            AccountHandlers.Register(dispatcher, flow);
            SessionHandlers.Register(dispatcher, entry, new ServerRequests(sessionSender), game);
            world = UnityWorld.Create(dispatcher, session, entry, options.Data, clock, log);
            session.Observer = flow;
            var reconnector = new Reconnector(flow);
            var timers = new List<ITickHandler> { game, new LoginWatchdog(flow.Progress) };
            world.AddTimers(timers);
            timers.Add(reconnector);
            var core = new GameClient(session, clock, timers.ToArray());
            return new LoginParts(flow, game, reconnector, core, new ClientOwner(world, session));
        }

        /// <summary>Закрывает клиент: отписывает мир от входа и закрывает сеть.</summary>
        private sealed class ClientOwner : IDisposable
        {
            private readonly IDisposable _world;
            private readonly IDisposable _session;

            public ClientOwner(IDisposable world, IDisposable session)
            {
                _world = world;
                _session = session;
            }

            public void Dispose()
            {
                _world.Dispose();
                _session.Dispose();
            }
        }

        // Соединение с сервером в обёртке симуляции пинга: свой генератор разброса на каждое соединение.
        private static IConnection NewConnection(IClock clock, LagSimulation lag) => new LaggedConnection(
            new TcpConnection(ArrayPool<byte>.Shared, ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes),
            clock, lag, new Random(), ArrayPool<byte>.Shared);

        // Seed логин-сервера: любое число, кроме 0 (NetState.cs: нулевой сервер отключает).
        private static int NewSeed() => SeedSource.Next(1, int.MaxValue);
    }
}
