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
    /// Перестроение маршрута и отказ при закрытом пути в действии path:x,y[,z],run|walk (PLAN Стадия 3, п. 11):
    /// внезапная преграда на пути, толчок/откат сервера, закрытая комната и недостижимая цель.
    /// </summary>
    public sealed class BotPathDynamicTests
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
            var check = new MovementCheck(_map, tiles, null, _store);
            var motion = new PlayerMotion(_store, check, _moves);
            var movement = new BotMovement(motion, new MovementReplies(motion, _moves, _log), _clock, check);
            movement.OnPlayerConfirmed(new PlayerState(Player, 0x190, Start, Direction.East, new MapSize(512, 512)));
            _visited = new List<Point3D>();
            motion.Moved += m => _visited.Add(m.To);
            _world = new BotWorld(_store, new WorldEnvironment(), new ContainerContents(_store), new ItemCommands(_store, new NoItems(), _clock),
                new SpeechCommands(new NoSpeech(), _clock), new TargetCursor(new NoTarget()))
            {
                Movement = movement,
            };
        }

        [Test]
        public void Path_MobStepsInFrontMidWalk_ReplansDetour()
        {
            var script = Script("path:108,100,run");
            var movement = _world.Movement!;
            bool spawned = false;

            for (int i = 0; i < 2000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                movement.Pacer.Update(_clock.NowMs);
                AnswerAll(movement);
                if (!spawned && movement.Motion.Position.X >= 102)
                {
                    spawned = true;
                    _store.UpdateMobile(new Serial(0x00000021), new MobileInfo(0x00EE, new Point3D(104, 100, 0), Direction.West, 0, 0, 1));
                }

                script.Step();
                _clock.NowMs += TickMs;
            }

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(spawned, Is.True);
            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(108, 100, 0)));
            Assert.That(_visited.Any(p => p.X == 104 && p.Y == 100), Is.False, "перестроил маршрут и обошёл крысу");
        }

        [Test]
        public void Path_ServerPushBackMidWalk_ReplansAndReachesTarget()
        {
            var script = Script("path:106,100,run");
            var movement = _world.Movement!;
            bool pushed = false;

            for (int i = 0; i < 2000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                movement.Pacer.Update(_clock.NowMs);
                AnswerAll(movement);
                if (!pushed && movement.Motion.Position.X == 103)
                {
                    pushed = true;
                    movement.Replies.OnServerPosition(new Point3D(101, 101, 0), Direction.East);
                }

                script.Step();
                _clock.NowMs += TickMs;
            }

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(pushed, Is.True);
            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(106, 100, 0)));
        }

        [Test]
        public void Path_ClosedRoom_FailsAsClosedWithoutSteps()
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

            var script = Script("path:105,100,run");
            RunUntilProblem(script);

            Assert.That(script.Problem, Does.Contain("путь закрыт"));
            Assert.That(_moves.Steps, Is.Empty, "при закрытом выходе шаги не уходят");
        }

        [Test]
        public void Path_WalledGoalFromOutside_FailsAtSearchLimitWithoutSteps()
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        _map.AddStatic(105 + dx, 100 + dy, new StaticTile(Wall, 0, 0));
                    }
                }
            }

            var script = Script("path:105,100,run");
            RunUntilProblem(script);

            Assert.That(script.Problem, Does.Contain("пределе поиска"));
            Assert.That(_moves.Steps, Is.Empty, "к закрытой цели шаги даже не уходят");
        }

        private void RunUntilProblem(BotScript script)
        {
            var movement = _world.Movement!;
            for (int i = 0; i < 2000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                movement.Pacer.Update(_clock.NowMs);
                AnswerAll(movement);
                script.Step();
                _clock.NowMs += TickMs;
            }

            Assert.That(script.Problem, Is.Not.Empty);
        }

        private void AnswerAll(BotMovement movement)
        {
            while (movement.Motion.PendingSteps > 0)
            {
                movement.Replies.OnAck(_moves.Steps[_moves.Steps.Count - movement.Motion.PendingSteps].Sequence, 1);
            }
        }

        private BotScript Script(params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), _world, _log, _clock);

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
