using System;
using System.Buffers;
using System.Collections.Generic;
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
    /// (NetworkSession, обработчики пакетов), вход, вход в мир, игровая сессия, мир с движением (<see cref="UnityWorld"/>) и
    /// ядро. Отличие от бота: повтор входа (<see cref="Reconnector"/>) в Unity включён всегда (ADR 0016 п. 6).
    /// </summary>
    public static class UnityLoginClient
    {
        private static readonly Random SeedSource = new Random();

        /// <param name="stop">Отмена закрывает соединение (выход из Play, закрытие игры).</param>
        /// <param name="data">Файлы данных; null — мир без ходьбы (шаг не предсказать без карты).</param>
        /// <param name="world">Мир этого входа: мобы и предметы по пакетам сервера, чужие мобы и своё движение.</param>
        public static LoginParts Create(IClock clock, ILog log, CancellationToken stop, UnityWorldData data, out UnityWorld world)
        {
            var dispatcher = new PacketDispatcher(log);
            var session = new NetworkSession(dispatcher, log);
            var connector = new ServerConnector(session, NewConnection, stop);
            var flow = new LoginFlow(new AccountSender(session), connector, NewSeed());
            var sessionSender = new SessionSender(session);
            var entry = new WorldEntry(flow.Progress, sessionSender);
            var game = new GameSession(flow.Progress, sessionSender, connector, clock);
            AccountHandlers.Register(dispatcher, flow);
            SessionHandlers.Register(dispatcher, entry, new ServerRequests(sessionSender), game);
            world = UnityWorld.Create(dispatcher, session, entry, data, clock, log);
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

        private static IConnection NewConnection() =>
            new TcpConnection(ArrayPool<byte>.Shared, ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes);

        // Seed логин-сервера: любое число, кроме 0 (NetState.cs: нулевой сервер отключает).
        private static int NewSeed() => SeedSource.Next(1, int.MaxValue);
    }
}
