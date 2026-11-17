using System;
using TheVerge.Core.Chat;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.Items;
using TheVerge.Core.Targeting;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.Combat;
using TheVerge.Core.Movement;
using TheVerge.Core.Status;
using TheVerge.Core.World;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Packets.Account;
using TheVerge.Network.Packets.Chat;
using TheVerge.Network.Packets.Items;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Packets.Combat;
using TheVerge.Network.Packets.Movement;
using TheVerge.Network.Packets.Status;
using TheVerge.Network.Packets.Targeting;
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
            InWorld = CreateWorld(dispatcher, Session, clock, login.Data, login.Lag, log);
            World.PlayerConfirmed += Entities.Reset;
            World.PlayerConfirmed += ResetCombat;
            if (InWorld.Movement != null)
            {
                World.PlayerConfirmed += InWorld.Movement.OnPlayerConfirmed;
            }
            Session.Observer = Flow;
            var watchdog = new LoginWatchdog(Flow.Progress);
            Reconnector = login.Reconnect ? new Reconnector(Flow) : null;
            Core = new GameClient(Session, clock, Timers(watchdog));
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

        /// <summary>
        /// Мир и действия в нём (PLAN Стадии 2–4): обработчики пакетов мира, движения (только с файлами данных), контейнеров,
        /// прицела, состояния мобов.
        /// </summary>
        private static BotWorld CreateWorld(PacketDispatcher dispatcher, NetworkSession session, IClock clock, BotWorldData? data,
            LagSimulation? lag, ILog log)
        {
            var entities = new EntityStore();
            var environment = new WorldEnvironment();
            var containers = new ContainerContents(entities);
            var movement = data == null ? null : CreateMovement(dispatcher, session, entities, data, clock, log);
            var others = new OthersMotion(entities, clock);
            WorldHandlers.Register(dispatcher, entities, environment, movement?.Replies, others);
            ItemHandlers.Register(dispatcher, containers);
            var actions = new ActionTimer();
            var items = new ItemCommands(entities, new ItemSender(session), clock, actions);
            var target = new TargetCursor(new TargetSender(session));
            TargetHandlers.Register(dispatcher, target);
            var status = new BotStatus(new MobileStatusUpdates(entities), new StatusCommands(new StatusSender(session)));
            StatusHandlers.Register(dispatcher, status.Updates);
            var combat = new CombatState();
            CombatHandlers.Register(dispatcher, combat);
            var journal = new CombatLog(clock);
            CombatLogHandlers.Register(dispatcher, journal);
            environment.SoundPlayed += journal.OnSound;
            var corpses = new CorpseEquipment(entities);
            CorpseHandlers.Register(dispatcher, corpses);
            return new BotWorld(entities, environment, containers, items, new SpeechCommands(new SpeechSender(session), clock), target, status)
            {
                QuickTargets = new TargetCommands(new QuickTargetSender(session), clock, actions),
                Combat = new BotCombat(combat, new CombatCommands(new CombatSender(session), combat, entities), journal),
                Corpses = corpses,
                Movement = movement,
                Others = new BotOthers(others, entities),
                Lag = lag,
            };
        }

        /// <summary>Движение своего персонажа (PLAN Стадия 3): предсказание по карте из --data и ответы сервера на шаги.</summary>
        private static BotMovement CreateMovement(PacketDispatcher dispatcher, NetworkSession session, EntityStore entities,
            BotWorldData data, IClock clock, ILog log)
        {
            var sender = new MovementSender(session);
            var check = new MovementCheck(data.Map, data.Tiles, data.Multis, entities);
            var motion = new PlayerMotion(entities, check, sender);
            var replies = new MovementReplies(motion, sender, log);
            MovementHandlers.Register(dispatcher, replies);
            return new BotMovement(motion, replies, clock, check);
        }

        /// <summary>Новый вход: стойку и противника сервер пришлёт заново, журнал боя — с чистого листа.</summary>
        private void ResetCombat(PlayerState player)
        {
            InWorld.Combat?.State.Reset();
            InWorld.Combat?.Log.Clear();
        }

        // Таймеры тика: ping и ожидания, очередь речи, показ шагов чужих, темп шагов (с --data), повтор входа (с --reconnect).
        private ITickHandler[] Timers(LoginWatchdog watchdog)
        {
            var timers = new System.Collections.Generic.List<ITickHandler> { Game, watchdog, InWorld.Speech };
            if (InWorld.Others != null)
            {
                timers.Add(InWorld.Others.Motion);
            }

            if (InWorld.Movement != null)
            {
                timers.Add(InWorld.Movement.Pacer);
            }

            if (Reconnector != null)
            {
                timers.Add(Reconnector);
            }

            return timers.ToArray();
        }

        // Seed логин-сервера: любое число, кроме 0 (NetState.cs: нулевой сервер отключает).
        private static int NewSeed() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);

        public void Dispose()
        {
            World.PlayerConfirmed -= Entities.Reset;
            World.PlayerConfirmed -= ResetCombat;
            if (InWorld.Movement != null)
            {
                World.PlayerConfirmed -= InWorld.Movement.OnPlayerConfirmed;
            }

            if (InWorld.Combat != null)
            {
                InWorld.Environment.SoundPlayed -= InWorld.Combat.Log.OnSound;
            }

            InWorld.Movement?.Dispose();
            InWorld.Others?.Dispose();
            Session.Dispose();
        }
    }
}
