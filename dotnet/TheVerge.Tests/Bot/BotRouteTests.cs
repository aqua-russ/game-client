using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Targeting;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Core.Movement;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действия бота для сценариев движения (PLAN Стадия 3, п. 10; tools/check/scenes.sh): goto:x,y,run|walk — дойти до
    /// клетки по прямой и диагонали (маршрут — несколько таких точек); blocked:&lt;направление&gt; — впереди преграда, шаг не
    /// уходит; resync — спросить сервер, где персонаж (0x22 → 0x20, Projects/UOContent/Network/Packets/
    /// IncomingPlayerPackets.cs, Resynchronize), и сверить с предсказанием клиента, включая высоту; at:x,y,z — ждать клетку
    /// с высотой.
    /// </summary>
    public sealed class BotRouteTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Point3D Start = new Point3D(100, 100, 0);
        private const int TickMs = 20;
        private const int Wall = 0x0002;

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private FakeMovementSender _moves = null!;
        private EntityStore _store = null!;
        private FakeMap _map = null!;
        private BotWorld _world = null!;
        private int _answeredResyncs;
        private Point3D? _serverPosition;

        [SetUp]
        public void SetUp()
        {
            _answeredResyncs = 0;
            _serverPosition = null;
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _moves = new FakeMovementSender();
            _store = new EntityStore();
            _store.Reset(new PlayerState(Player, 0x190, Start, Direction.East, new MapSize(512, 512)));
            _store.ChangeMap(0);
            _map = new FakeMap(512, 512, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            tiles.SetItem(Wall, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            var motion = new PlayerMotion(_store, new MovementCheck(_map, tiles, null, _store), _moves);
            var movement = new BotMovement(motion, new MovementReplies(motion, _moves, _log), _clock);
            movement.OnPlayerConfirmed(new PlayerState(Player, 0x190, Start, Direction.East, new MapSize(512, 512)));
            _world = new BotWorld(_store, new WorldEnvironment(), new ContainerContents(_store), new ItemCommands(_store, new NoItems(), _clock),
                new SpeechCommands(new NoSpeech(), _clock), new TargetCursor(new NoTarget()))
            {
                Movement = movement,
            };
        }

        [TestCase("goto:104,102,run", true)]
        [TestCase("goto:104,102,walk", true)]
        [TestCase("goto:104,102", false)]
        [TestCase("goto:104,102,fly", false)]
        [TestCase("goto:a,102,run", false)]
        [TestCase("blocked:e", true)]
        [TestCase("blocked:nw", true)]
        [TestCase("blocked:east", false)]
        [TestCase("blocked", false)]
        [TestCase("resync", true)]
        [TestCase("resync:1", false)]
        [TestCase("at:10,20", true)]
        [TestCase("at:10,20,5", true)]
        [TestCase("at:10,20,-5", true)]
        [TestCase("at:10,20,x", false)]
        [TestCase("near:0x06A5", true)]
        [TestCase("near:06A5", false)]
        [TestCase("near", false)]
        public void Parse(string text, bool valid)
        {
            Assert.That(BotAction.Parse(text, out _) != null, Is.EqualTo(valid));
        }

        [Test]
        public void Goto_DiagonalThenStraight_ReachesTarget()
        {
            var script = Script("goto:104,102,run");

            RunUntilDone(script);

            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(104, 102, 0)));
            Assert.That(_moves.Steps.Select(s => s.Direction), Is.EqualTo(new[]
            {
                Direction.Down, Direction.Down, Direction.Down, Direction.East, Direction.East, Direction.East
            }), "поворот на юго-восток, два шага, поворот на восток, два шага");
            Assert.That(_moves.Steps.TrueForAll(s => s.Run));
            Assert.That(Text(), Does.Contain("Маршрут до (104, 102), бег: шагов 4, отказов сервера 0, ресинхронизаций 0."));
        }

        [Test]
        public void Goto_Walking_StepsWithoutRun()
        {
            var script = Script("goto:100,97,walk");

            RunUntilDone(script);

            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(100, 97, 0)));
            Assert.That(_moves.Steps.TrueForAll(s => !s.Run));
            Assert.That(Text(), Does.Contain("Маршрут до (100, 97), шаг: шагов 3"));
        }

        [Test]
        public void Goto_AlreadyThere_NoSteps()
        {
            var script = Script("goto:100,100,run");

            RunUntilDone(script);

            Assert.That(_moves.Steps, Is.Empty);
        }

        [Test]
        public void Goto_WallInTheWay_FailsAndSaysWhere()
        {
            _map.AddStatic(102, 100, new StaticTile(Wall, 0, 0));
            var script = Script("goto:105,100,run");

            RunUntilProblem(script);

            Assert.That(script.Problem, Does.Contain("путь закрыт"));
            Assert.That(script.Problem, Does.Contain("(101, 100, 0)"));
            Assert.That(_moves.Steps.Count, Is.EqualTo(1), "один шаг до стены, в стену шаг не уходит");
        }

        [Test]
        public void Blocked_WallAhead_NoStepGoes()
        {
            _map.AddStatic(101, 100, new StaticTile(Wall, 0, 0));
            var script = Script("blocked:e");

            RunUntilDone(script);

            Assert.That(_moves.Steps, Is.Empty);
            Assert.That(_store.Player!.Position, Is.EqualTo(Start));
            Assert.That(Text(), Does.Contain("Преграда e: шаг не ушёл."));
        }

        [Test]
        public void Blocked_DifferentFacing_OnlyTurnGoes()
        {
            _map.AddStatic(100, 99, new StaticTile(Wall, 0, 0));
            var script = Script("blocked:n");

            RunUntilDone(script);

            Assert.That(_moves.Steps.Select(s => s.Direction), Is.EqualTo(new[] { Direction.North }), "только поворот");
            Assert.That(_store.Player!.Position, Is.EqualTo(Start));
        }

        [Test]
        public void Blocked_OpenWay_Fails()
        {
            var script = Script("blocked:e");

            RunUntilProblem(script);

            Assert.That(script.Problem, Does.Contain("шаг ушёл"));
        }

        [Test]
        public void Resync_AfterWalk_ServerAgrees_Logged()
        {
            var script = Script("goto:103,100,run", "resync");

            RunUntilDone(script);

            Assert.That(_moves.Resyncs, Is.EqualTo(1));
            Assert.That(Text(), Does.Contain("Сверка с сервером: персонаж в (103, 100, 0), как предсказано."));
        }

        [Test]
        public void Resync_ServerDisagrees_Fails()
        {
            _serverPosition = new Point3D(100, 100, 5);
            var script = Script("resync");

            RunUntilProblem(script);

            Assert.That(script.Problem, Does.Contain("сервер ставит персонажа в (100, 100, 5)"));
            Assert.That(script.Problem, Does.Contain("клиент считал (100, 100, 0)"));
        }

        [Test]
        public void WaitAt_WithHeight_WaitsForHeight()
        {
            var wrong = Script("at:100,100,5");
            wrong.Step();
            Assert.That(wrong.IsDone, Is.False);

            var right = Script("at:100,100,0");
            right.Step();
            Assert.That(right.IsDone, Is.True);
        }

        [Test]
        public void Near_WaitsForItemWithinTwoCells()
        {
            var script = Script("near:0x06A6");
            _store.PlaceInWorld(new Serial(0x40000050), new ItemInfo(0x06A6, 1, new Point3D(103, 100, 0), 0, 0, 0));
            script.Step();
            Assert.That(script.IsDone, Is.False, "3 клетки — далеко");

            _store.PlaceInWorld(new Serial(0x40000051), new ItemInfo(0x06A6, 1, new Point3D(99, 102, 0), 0, 0, 0));
            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(Text(), Does.Contain("Рядом предмет 0x06A6: 0x40000051 в (99, 102, 0)."));
        }

        // Тик клиента — темп шагов; сервер отвечает сразу (и на запрос сверки — своим местом); потом действие бота.
        private void Run(BotScript script)
        {
            var movement = _world.Movement!;
            for (int i = 0; i < 2000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                movement.Pacer.Update(_clock.NowMs);
                while (movement.Motion.PendingSteps > 0)
                {
                    movement.Replies.OnAck(_moves.Steps[_moves.Steps.Count - movement.Motion.PendingSteps].Sequence, 1);
                }

                if (_moves.Resyncs > _answeredResyncs)
                {
                    _answeredResyncs = _moves.Resyncs;
                    movement.Replies.OnServerPosition(_serverPosition ?? _store.Player!.Position, Direction.East);
                }

                script.Step();
                _clock.NowMs += TickMs;
            }
        }

        private void RunUntilDone(BotScript script)
        {
            Run(script);
            Assert.That(script.IsDone, Is.True, script.Problem);
        }

        private void RunUntilProblem(BotScript script)
        {
            Run(script);
            Assert.That(script.Problem, Is.Not.Empty);
        }

        private BotScript Script(params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), _world, _log, _clock);

        private string Text() => string.Join("\n", _log.Entries.Select(e => e.Message));

        private sealed class NoItems : TheVerge.Core.Ports.IItemSender
        {
            public void SendUse(Serial serial)
            {
            }
        }

        private sealed class NoSpeech : TheVerge.Core.Ports.ISpeechSender
        {
            public void SendSpeech(string text, ushort hue)
            {
            }
        }

        private sealed class NoTarget : TheVerge.Core.Ports.ITargetSender
        {
            public void SendTarget(in TargetResponse response)
            {
            }
        }
    }
}
