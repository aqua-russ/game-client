using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.Session.Screens
{
    /// <summary>
    /// Экраны входа с клиентом в памяти: на каждое нажатие «Войти» — новый клиент (как в Unity), тест играет сервер
    /// вызовами LoginFlow и видит, что ушло серверу и закрыт ли клиент.
    /// </summary>
    internal sealed class ScreensHarness
    {
        public ScreensHarness()
        {
            Screens = new LoginScreens(NewClient);
        }

        public FakeClock Clock { get; } = new FakeClock();

        public LoginScreens Screens { get; }

        public List<Client> Clients { get; } = new List<Client>();

        public Client Last => Clients[Clients.Count - 1];

        public void Tick(long advanceMs = 16)
        {
            Clock.NowMs += advanceMs;
            Screens.Tick(Clock.NowMs);
        }

        public void SubmitLogin()
        {
            Screens.Form.Username = "player";
            Screens.Form.Password = "secret";
            Screens.Submit();
        }

        /// <summary>Сервер принимает логин и присылает шарды.</summary>
        public void ToShards(int shardCount = 1)
        {
            SubmitLogin();
            Last.Flow.OnConnected();
            var shards = new ShardEntry[shardCount];
            for (var i = 0; i < shardCount; i++)
            {
                shards[i] = new ShardEntry(i, "Shard " + (i + 1), 10, 0, "127.0.0.1");
            }

            Last.Flow.OnShardList(shards);
            Tick();
        }

        /// <summary>До списка персонажей: слоты по именам (пусто — пустой слот), один город.</summary>
        public void ToCharacters(params string[] names)
        {
            ToShards();
            Screens.ChooseShard(0);
            Last.Flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 7));
            Last.Flow.OnConnected();
            var slots = new CharacterSlot[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                slots[i] = new CharacterSlot(i, names[i]);
            }

            Last.Flow.Characters.OnCharacterList(slots, new[] { new StartingCity(0, "Britain", "Inn", new Point3D(1, 2, 3), 0, 0) }, 0);
            Tick();
        }

        /// <summary>В мир первым персонажем.</summary>
        public void ToWorld()
        {
            ToCharacters("Борис", string.Empty);
            Screens.Characters.Select(0);
            Screens.Characters.Enter();
            EnterWorldOnServer();
        }

        public void EnterWorldOnServer()
        {
            Last.World.OnLoginConfirm(new PlayerState(new Serial(1), 0x190, Point3D.Zero, Direction.North, new MapSize(7168, 4096)));
            Last.World.OnLoginComplete();
            Tick();
        }

        private LoginParts NewClient()
        {
            var client = new Client(Clock);
            Clients.Add(client);
            return client.Parts;
        }

        internal sealed class Client : IDisposable
        {
            public Client(FakeClock clock)
            {
                Flow = new LoginFlow(Account, Connector, 1);
                World = new WorldEntry(Flow.Progress, Session);
                var game = new GameSession(Flow.Progress, Session, Connector, clock);
                var reconnector = new Reconnector(Flow);
                var core = new GameClient(new FakeNetworkSession(clock), clock, game, new LoginWatchdog(Flow.Progress), reconnector);
                Parts = new LoginParts(Flow, game, reconnector, core, this);
            }

            public RecordingAccountSender Account { get; } = new RecordingAccountSender();

            public RecordingSessionSender Session { get; } = new RecordingSessionSender();

            public RecordingConnector Connector { get; } = new RecordingConnector();

            public LoginFlow Flow { get; }

            public WorldEntry World { get; }

            public LoginParts Parts { get; }

            public bool Disposed { get; private set; }

            public void Dispose() => Disposed = true;
        }
    }
}
