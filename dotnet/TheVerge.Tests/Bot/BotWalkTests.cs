using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
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
    /// Действия бота для живой ходьбы (PLAN Стадия 3, п. 7; tools/check/walk.sh): walk:&lt;направление&gt;,&lt;шагов&gt;,run|walk —
    /// держать клавиши, пока сервер не подтвердит шаги, и написать отказы и темп; mounted — ждать коня; use: находит предмет
    /// и в своём рюкзаке (эфирного коня сервер разрешает только при себе — EtherealMount.Validate).
    /// </summary>
    public sealed class BotWalkTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Backpack = new Serial(0x400001E2);
        // Тик делит задержки шагов (200, 400) нацело: шаги уходят ровно по расписанию, и темп в журнале точный.
        private const int TickMs = 20;

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private RecordingItemSender _items = null!;
        private FakeMovementSender _moves = null!;
        private EntityStore _store = null!;
        private BotWorld _world = null!;
        private int _rejectAtMove = -1;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _items = new RecordingItemSender();
            _moves = new FakeMovementSender();
            _store = new EntityStore();
            _store.Reset(new PlayerState(Player, 0x190, new Point3D(100, 100, 0), Direction.East, new MapSize(512, 512)));
            _store.ChangeMap(0);
            var map = new FakeMap(512, 512, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            var motion = new PlayerMotion(_store, new MovementCheck(map, tiles, null, _store), _moves);
            var movement = new BotMovement(motion, new MovementReplies(motion, _moves, _log), _clock);
            movement.OnPlayerConfirmed(new PlayerState(Player, 0x190, new Point3D(100, 100, 0), Direction.East, new MapSize(512, 512)));
            _world = World(movement);
        }

        [TestCase("walk:e,40,run", true)]
        [TestCase("walk:nw,1,walk", true)]
        [TestCase("walk:north,5,run", false)]
        [TestCase("walk:e,0,run", false)]
        [TestCase("walk:e,5,fly", false)]
        [TestCase("walk:e,5", false)]
        [TestCase("mounted", true)]
        [TestCase("mounted:1", false)]
        public void Parse(string text, bool valid)
        {
            Assert.That(BotAction.Parse(text, out _) != null, Is.EqualTo(valid));
        }

        [Test]
        public void Walk_WithoutData_FailsAtOnce()
        {
            _world = World(null);
            var script = Script("walk:e,5,run");

            script.Step();

            Assert.That(script.Problem, Does.Contain("--data"));
        }

        [Test]
        public void Walk_Run_StepsAtServerPace_ReportsTempo()
        {
            var script = Script("walk:e,10,run");

            RunUntilDone(script);

            Assert.That(script.Problem, Is.Empty);
            Assert.That(_store.Player!.Position.X, Is.GreaterThanOrEqualTo(110));
            Assert.That(_moves.Steps.TrueForAll(s => s.Run && s.Direction == Direction.East));
            Assert.That(Text(), Does.Contain("Ходьба e, бег пешком: шагов 10, отказов сервера 0, ресинхронизаций 0; " +
                                             "пауза между шагами в среднем 200 мс (5,00 шага в секунду)"));
        }

        [Test]
        public void Walk_Walking_TogglesRunOff()
        {
            var script = Script("walk:w,3,walk");

            RunUntilDone(script);

            Assert.That(_moves.Steps.TrueForAll(s => !s.Run));
            Assert.That(Text(), Does.Contain("Ходьба w, шаг пешком: шагов 3"));
            Assert.That(Text(), Does.Contain("в среднем 400 мс (2,50 шага в секунду)"));
        }

        [Test]
        public void Walk_ServerReject_CountedAndWalkFinishes()
        {
            _rejectAtMove = 4;
            var script = Script("walk:e,8,run");

            RunUntilDone(script);

            Assert.That(script.Problem, Is.Empty);
            Assert.That(Text(), Does.Contain("шагов 8, отказов сервера 1"));
        }

        [Test]
        public void Mounted_WaitsForMountLayer()
        {
            var script = Script("mounted");
            script.Step();
            Assert.That(script.IsDone, Is.False);

            _store.Equip(new Serial(0x40000010), new ItemInfo(0x3EAA, 1, Point3D.Zero, 0, 0, 0), Player, Layer.Mount);
            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(Text(), Does.Contain("Верхом: 0x3EAA."));
        }

        [Test]
        public void UseItem_FoundInOwnBackpack()
        {
            _store.Equip(Backpack, new ItemInfo(0x0E75, 1, Point3D.Zero, 0, 0, 0), Player, Layer.Backpack);
            _world.Containers.AddContent(new Serial(0x40000300), new ItemInfo(0x20DD, 1, new Point3D(40, 60, 0), 0, 0, 0), Backpack, 0);
            var script = Script("use:0x20DD");

            script.Step();

            Assert.That(_items.Used, Is.EqualTo(new[] { 0x40000300u }));
        }

        // Тик клиента — темп шагов; сервер отвечает сразу; потом действие бота, как в BotRunner.
        private void RunUntilDone(BotScript script)
        {
            var movement = _world.Movement!;
            for (int i = 0; i < 2000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                movement.Pacer.Update(_clock.NowMs);
                AnswerAll(movement);
                script.Step();
                _clock.NowMs += TickMs;
            }

            Assert.That(script.IsDone, Is.True, script.Problem);
        }

        private void AnswerAll(BotMovement movement)
        {
            while (movement.Motion.PendingSteps > 0)
            {
                var step = _moves.Steps[_moves.Steps.Count - movement.Motion.PendingSteps];
                if (_moves.Steps.Count == _rejectAtMove + 1)
                {
                    _rejectAtMove = -1;
                    var at = _store.Player!.Position;
                    movement.Replies.OnReject(step.Sequence, at, Direction.East);
                    continue;
                }

                movement.Replies.OnAck(step.Sequence, 1);
            }
        }

        private BotWorld World(BotMovement? movement) =>
            new BotWorld(_store, new WorldEnvironment(), new ContainerContents(_store), new ItemCommands(_store, _items, _clock),
                new SpeechCommands(new NoSpeech(), _clock), new TargetCursor(new NoTarget()))
            {
                Movement = movement,
            };

        private BotScript Script(params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), _world, _log, _clock);

        private string Text() => string.Join("\n", _log.Entries.Select(e => e.Message));

        private sealed class RecordingItemSender : IItemSender
        {
            public List<uint> Used { get; } = new List<uint>();

            public void SendUse(Serial serial) => Used.Add(serial.Value);
        }

        private sealed class NoSpeech : ISpeechSender
        {
            public void SendSpeech(string text, ushort hue)
            {
            }
        }

        private sealed class NoTarget : ITargetSender
        {
            public void SendTarget(in TargetResponse response)
            {
            }
        }
    }
}
