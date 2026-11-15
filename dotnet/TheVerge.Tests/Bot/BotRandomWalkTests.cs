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
    /// randomwalk:&lt;шагов&gt;,&lt;номер&gt;,&lt;x0&gt;,&lt;y0&gt;,&lt;x1&gt;,&lt;y1&gt;,run|walk|mix (PLAN Стадия 3, п. 10,
    /// random-walk; tools/check/randomwalk.sh): бот держит клавиши случайной стороны 1–8 шагов, выбирая только те стороны,
    /// куда пускает проверка шага клиента и которые не выводят из квадрата. Один номер — одна и та же прогулка.
    /// </summary>
    public sealed class BotRandomWalkTests
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
        private List<Point3D> _visited = null!;
        private HashSet<(int, int)> _walls = null!;

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
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            tiles.SetItem(Wall, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            var motion = new PlayerMotion(_store, new MovementCheck(_map, tiles, null, _store), _moves);
            var movement = new BotMovement(motion, new MovementReplies(motion, _moves, _log), _clock);
            movement.OnPlayerConfirmed(new PlayerState(Player, 0x190, Start, Direction.East, new MapSize(512, 512)));
            _visited = new List<Point3D>();
            _walls = new HashSet<(int, int)>();
            motion.Moved += move => _visited.Add(move.To);
            _world = new BotWorld(_store, new WorldEnvironment(), new ContainerContents(_store), new ItemCommands(_store, new NoItems(), _clock),
                new SpeechCommands(new NoSpeech(), _clock), new TargetCursor(new NoTarget()))
            {
                Movement = movement,
            };
        }

        [TestCase("randomwalk:1000,7,90,90,110,110,mix", true)]
        [TestCase("randomwalk:1,0,90,90,110,110,run", true)]
        [TestCase("randomwalk:10,7,90,90,110,110,walk", true)]
        [TestCase("randomwalk:0,7,90,90,110,110,mix", false)]
        [TestCase("randomwalk:10,7,90,90,110,110,fly", false)]
        [TestCase("randomwalk:10,7,110,90,90,110,mix", false)]
        [TestCase("randomwalk:10,7,90,90,110,mix", false)]
        [TestCase("randomwalk:10,x,90,90,110,110,mix", false)]
        public void Parse(string text, bool valid)
        {
            Assert.That(BotAction.Parse(text, out _) != null, Is.EqualTo(valid));
        }

        [Test]
        public void StaysInSquare_NeverStepsIntoWalls_CountsSteps()
        {
            AddWalls();
            var script = Script("randomwalk:300,7,95,95,108,106,mix");

            RunUntilDone(script);

            Assert.That(_visited.Count, Is.EqualTo(300));
            Assert.That(_visited.TrueForAll(p => p.X >= 95 && p.X <= 108 && p.Y >= 95 && p.Y <= 106), "не выходит из квадрата");
            Assert.That(_visited.TrueForAll(p => !_walls.Contains((p.X, p.Y))), "в стены не шагает");
            Assert.That(_visited.Select(p => (p.X, p.Y)).Distinct().Count(), Is.GreaterThan(60), "обходит квадрат, а не топчется");
            Assert.That(Text(), Does.Contain("Случайная ходьба: шагов 300"));
            Assert.That(Text(), Does.Contain("отказов сервера 0, ресинхронизаций 0"));
        }

        [Test]
        public void SameNumber_SameWalk()
        {
            AddWalls();
            RunUntilDone(Script("randomwalk:120,42,95,95,108,106,mix"));
            var first = _visited.ToList();

            SetUp();
            AddWalls();
            RunUntilDone(Script("randomwalk:120,42,95,95,108,106,mix"));

            Assert.That(_visited, Is.EqualTo(first));
        }

        [Test]
        public void Mix_RunsAndWalks_WalkOnlyWalks()
        {
            RunUntilDone(Script("randomwalk:200,3,90,90,110,110,mix"));
            Assert.That(_moves.Steps.Any(s => s.Run) && _moves.Steps.Any(s => !s.Run), "вперемешку бег и шаг");

            SetUp();
            RunUntilDone(Script("randomwalk:50,3,90,90,110,110,walk"));
            Assert.That(_moves.Steps.TrueForAll(s => !s.Run));
        }

        [Test]
        public void WalledIn_FailsAsStuck()
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        _map.AddStatic(100 + dx, 100 + dy, new StaticTile(Wall, 0, 0));
                    }
                }
            }

            var script = Script("randomwalk:10,1,90,90,110,110,run");
            Run(script);

            Assert.That(script.Problem, Does.Contain("застрял"));
            Assert.That(script.Problem, Does.Contain("(100, 100, 0)"));
        }

        // Стенки внутри квадрата: две полосы с проходами.
        private void AddWalls()
        {
            for (int y = 96; y <= 104; y++)
            {
                if (y != 100)
                {
                    AddWall(99, y);
                }
            }

            for (int x = 101; x <= 107; x++)
            {
                if (x != 104)
                {
                    AddWall(x, 103);
                }
            }
        }

        private void AddWall(int x, int y)
        {
            _map.AddStatic(x, y, new StaticTile(Wall, 0, 0));
            _walls.Add((x, y));
        }

        private void Run(BotScript script)
        {
            var movement = _world.Movement!;
            for (int i = 0; i < 40000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                movement.Pacer.Update(_clock.NowMs);
                while (movement.Motion.PendingSteps > 0)
                {
                    movement.Replies.OnAck(_moves.Steps[_moves.Steps.Count - movement.Motion.PendingSteps].Sequence, 1);
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
