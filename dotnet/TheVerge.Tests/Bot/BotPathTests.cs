using System.Collections.Generic;
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
    /// Разбор и базовое выполнение path:x,y[,z],run|walk (PLAN Стадия 3, п. 11): обход стен и мобов, подъём по Z.
    /// </summary>
    public sealed class BotPathTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Point3D Start = new Point3D(100, 100, 0);
        private const int TickMs = 20;
        private const int Wall = 0x0002;
        private const int Floor = 0x0001;
        private const int TallStair = 0x0016;
        private const int MidStair = 0x0017;

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private FakeMovementSender _moves = null!;
        private EntityStore _store = null!;
        private FakeMap _map = null!;
        private FakeTiles _tiles = null!;
        private MovementCheck _check = null!;
        private BotWorld _world = null!;
        private List<Point3D> _visited = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _moves = new FakeMovementSender();
            _store = new EntityStore();
            _store.Reset(new PlayerState(Player, 0x190, Start, Direction.East, new MapSize(512, 512)));
            _store.ChangeMap(0);
            _map = new FakeMap(512, 512, new LandTile(3, 0));
            _tiles = new FakeTiles();
            _tiles.SetLand(3, TileFlags.None, "grass");
            _tiles.SetItem(Floor, TileFlags.Surface, 0, "floor");
            _tiles.SetItem(Wall, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            _tiles.SetItem(TallStair, TileFlags.Surface | TileFlags.Bridge, 16, "tallStair");
            _tiles.SetItem(MidStair, TileFlags.Surface | TileFlags.Bridge, 8, "midStair");
            _check = new MovementCheck(_map, _tiles, null, _store);
            var motion = new PlayerMotion(_store, _check, _moves);
            var movement = new BotMovement(motion, new MovementReplies(motion, _moves, _log), _clock, _check);
            movement.OnPlayerConfirmed(new PlayerState(Player, 0x190, Start, Direction.East, new MapSize(512, 512)));
            _visited = new List<Point3D>();
            motion.Moved += m => _visited.Add(m.To);
            _world = World(movement);
        }

        [TestCase("path:104,102,run", true)]
        [TestCase("path:104,102,walk", true)]
        [TestCase("path:104,102,20,run", true)]
        [TestCase("path:104,102,-5,walk", true)]
        [TestCase("path:104,102", false)]
        [TestCase("path:104,102,fly", false)]
        [TestCase("path:104,102,200,run", false)]
        [TestCase("path:a,102,run", false)]
        [TestCase("path:", false)]
        public void Parse(string text, bool valid)
        {
            Assert.That(BotAction.Parse(text, out _) != null, Is.EqualTo(valid));
        }

        [Test]
        public void Path_WithoutData_FailsClearly()
        {
            _world = World(null);
            var script = Script("path:104,100,run");

            script.Step();

            Assert.That(script.Problem, Does.Contain("--data"));
        }

        [Test]
        public void Path_AroundWallAndMob_ReachesTargetWithoutRejects()
        {
            for (int y = 98; y <= 102; y++)
            {
                _map.AddStatic(102, y, new StaticTile(Wall, 0, 0));
            }

            var rat = new Serial(0x00000020);
            _store.UpdateMobile(rat, new MobileInfo(0x00EE, new Point3D(102, 97, 0), Direction.West, 0, 0, 1));

            var script = Script("path:104,100,run");
            RunUntilDone(script);

            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(104, 100, 0)));
            Assert.That(_visited.Any(p => p.X == 102 && p.Y == 97), Is.False, "обходит моба, не наступает на него");
            Assert.That(_world.Movement!.Replies.RejectCount, Is.EqualTo(0));
            Assert.That(Text(), Does.Contain("Путь A* до (104, 100)"));
            Assert.That(Text(), Does.Contain("отказов сервера 0, ресинхронизаций 0"));
        }

        [Test]
        public void Path_WithTargetZ_ClimbsStairsToUpperFloor()
        {
            for (int y = 98; y <= 103; y++)
            {
                if (y != 102)
                {
                    _map.AddStatic(102, y, new StaticTile(Wall, 0, 0));
                }
            }

            _map.AddStatic(102, 102, new StaticTile(TallStair, 0, 0));  // Z = 8
            _map.AddStatic(103, 102, new StaticTile(TallStair, 8, 0));  // Z = 16
            _map.AddStatic(104, 102, new StaticTile(MidStair, 16, 0));  // Z = 20
            _map.AddStatic(104, 101, new StaticTile(Floor, 20, 0));
            _map.AddStatic(104, 100, new StaticTile(Floor, 20, 0));

            var script = Script("path:104,100,20,walk");
            RunUntilDone(script);

            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(104, 100, 20)));
            Assert.That(_moves.Steps.TrueForAll(s => !s.Run), Is.True);
        }

        [Test]
        public void Path_AlreadyThere_CompletesWithZeroSteps()
        {
            var script = Script("path:100,100,run");

            RunUntilDone(script);

            Assert.That(_moves.Steps, Is.Empty);
        }

        private void RunUntilDone(BotScript script)
        {
            var movement = _world.Movement!;
            for (int i = 0; i < 2000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                movement.Pacer.Update(_clock.NowMs);
                while (movement.Motion.PendingSteps > 0)
                {
                    movement.Replies.OnAck(_moves.Steps[_moves.Steps.Count - movement.Motion.PendingSteps].Sequence, 1);
                }

                script.Step();
                _clock.NowMs += TickMs;
            }

            Assert.That(script.IsDone, Is.True, script.Problem);
        }

        private BotWorld World(BotMovement? movement) =>
            new BotWorld(_store, new WorldEnvironment(), new ContainerContents(_store), new ItemCommands(_store, new NoItems(), _clock),
                new SpeechCommands(new NoSpeech(), _clock), new TargetCursor(new NoTarget()))
            {
                Movement = movement,
            };

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
