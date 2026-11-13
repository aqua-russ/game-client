using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
using TheVerge.Core.Targeting;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действия бота в мире (--action), по очереди: каждое ждёт своего ответа сервера не дольше <see cref="BotScript.ActionTimeoutMs"/>.
    /// use:backpack — двойной клик по своему рюкзаку, ждать 0x24 и 0x3C, написать содержимое в лог (PLAN Стадия 2, п. 9а).
    /// </summary>
    public sealed class BotScriptTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Backpack = new Serial(0x400001E2);

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private RecordingItemSender _sender = null!;
        private RecordingSpeechSender _speech = null!;
        private RecordingTargetSender _targets = null!;
        private BotWorld _world = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _sender = new RecordingItemSender();
            _speech = new RecordingSpeechSender();
            _targets = new RecordingTargetSender();
            var store = new EntityStore();
            _world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store), new ItemCommands(store, _sender, _clock),
                new SpeechCommands(_speech, _clock), new TargetCursor(_targets));
            store.Reset(new PlayerState(Player, 0x190, new Point3D(1, 1, 0), Direction.North, new MapSize(7168, 4096)));
        }

        [Test]
        public void NoActions_DoneAtOnce()
        {
            var script = Script();

            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(script.Problem, Is.Empty);
        }

        [Test]
        public void UseBackpack_OpensWaitsForContent_LogsIt()
        {
            _world.Entities.Equip(Backpack, new ItemInfo(0x0E75, 1, Point3D.Zero, 0, 0, 0), Player, Layer.Backpack);
            var script = Script("use:backpack");

            script.Step();
            Assert.That(_sender.Used, Is.EqualTo(new[] { Backpack.Value }));
            Assert.That(script.IsDone, Is.False);

            _world.Containers.OnOpen(Backpack, 0x3C);
            _world.Containers.BeginContent(Backpack);
            _world.Containers.AddContent(new Serial(0x40000300), new ItemInfo(0x0EED, 100, new Point3D(40, 60, 0), 0, 0, 0), Backpack, 0);
            _world.Containers.EndContent();
            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(_sender.Used.Count, Is.EqualTo(1));
            Assert.That(Text(), Does.Contain("Рюкзак 0x400001E2 открыт: предметов 1 (0x0EED×100)."));
        }

        [Test]
        public void UseBackpack_NoBackpack_ProblemAfterTimeout()
        {
            var script = Script("use:backpack");

            script.Step();
            _clock.NowMs += BotScript.ActionTimeoutMs;
            script.Step();

            Assert.That(script.Problem, Does.Contain("рюкзак"));
        }

        [Test]
        public void UseBackpack_NoAnswer_ProblemAfterTimeout()
        {
            _world.Entities.Equip(Backpack, new ItemInfo(0x0E75, 1, Point3D.Zero, 0, 0, 0), Player, Layer.Backpack);
            var script = Script("use:backpack");

            script.Step();
            _clock.NowMs += BotScript.ActionTimeoutMs;
            script.Step();

            Assert.That(script.Problem, Does.Contain("не открыл"));
        }

        [Test]
        public void Say_TextSent()
        {
            var script = Script("say:[go 1500 1600");

            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(_speech.Sent, Is.EqualTo(new[] { "[go 1500 1600" }));
        }

        [Test]
        public void At_WaitsUntilPlayerInCell()
        {
            var script = Script("at:1500,1600");

            script.Step();
            Assert.That(script.IsDone, Is.False);

            _world.Entities.MovePlayerTo(new Point3D(1500, 1600, 5));
            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(Text(), Does.Contain("Персонаж в клетке (1500, 1600, 5)."));
        }

        [Test]
        public void At_NotReached_ProblemAfterTimeout()
        {
            var script = Script("at:1500,1600");

            script.Step();
            _clock.NowMs += BotScript.ActionTimeoutMs;
            script.Step();

            Assert.That(script.Problem, Does.Contain("(1500, 1600)").And.Contain("(1, 1, 0)"));
        }

        [Test]
        public void TargetHere_WaitsForCursor_AnswersPlayersCell()
        {
            var script = Script("target:here");

            script.Step();
            Assert.That(_targets.Responses, Is.Empty, "курсора ещё нет");

            _world.Target.OnRequest(true, 42, 0);
            script.Step();

            Assert.That(script.IsDone, Is.True);
            var r = _targets.Responses.Single();
            Assert.That(r.Kind, Is.EqualTo(TargetKind.Location));
            Assert.That(r.CursorId, Is.EqualTo(42));
            Assert.That(r.Position, Is.EqualTo(new Point3D(1, 1, 0)));
            Assert.That(r.Graphic, Is.EqualTo((ushort)0), "земля");
        }

        [Test]
        public void TargetSelf_AnswersPlayerSerial()
        {
            var script = Script("target:self");
            _world.Target.OnRequest(false, 5, 0);

            script.Step();

            Assert.That(_targets.Responses.Single().Serial, Is.EqualTo(Player));
        }

        [Test]
        public void TargetCell_LocationNotAllowed_Problem()
        {
            var script = Script("target:1500,1600,-5");
            _world.Target.OnRequest(false, 5, 0);

            script.Step();

            Assert.That(script.Problem, Does.Contain("только объект"));
            Assert.That(_targets.Responses, Is.Empty);
        }

        [Test]
        public void TargetCancel_SendsCancel()
        {
            var script = Script("target:cancel");
            _world.Target.OnRequest(true, 5, 0);

            script.Step();

            Assert.That(_targets.Responses.Single().Position.X, Is.EqualTo(-1));
        }

        [Test]
        public void TargetNoCursor_ProblemAfterTimeout()
        {
            var script = Script("target:here");

            script.Step();
            _clock.NowMs += BotScript.ActionTimeoutMs;
            script.Step();

            Assert.That(script.Problem, Does.Contain("курсор"));
        }

        [Test]
        public void Seen_WaitsForItemOnGroundAtFeet()
        {
            var script = Script("seen:0x09D0");
            _world.Entities.PlaceInWorld(new Serial(0x40000700), new ItemInfo(0x09D0, 1, new Point3D(2, 1, 0), 0, 0, 0));

            script.Step();
            Assert.That(script.IsDone, Is.False, "яблоко в соседней клетке");

            _world.Entities.PlaceInWorld(new Serial(0x40000701), new ItemInfo(0x09D0, 1, new Point3D(1, 1, 0), 0, 0, 0));
            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(Text(), Does.Contain("Предмет 0x09D0 на земле: 0x40000701 в (1, 1, 0)."));
        }

        private BotScript Script(params string[] actions)
        {
            var parsed = actions.Select(a => BotAction.Parse(a, out _)!).ToList();
            return new BotScript(parsed, _world, _log, _clock);
        }

        private string Text() => string.Join("\n", _log.Entries.Select(e => e.Message));

        private sealed class RecordingTargetSender : ITargetSender
        {
            public List<TargetResponse> Responses { get; } = new List<TargetResponse>();

            public void SendTarget(in TargetResponse response) => Responses.Add(response);
        }

        private sealed class RecordingSpeechSender : ISpeechSender
        {
            public List<string> Sent { get; } = new List<string>();

            public void SendSpeech(string text, ushort hue) => Sent.Add(text);
        }

        private sealed class RecordingItemSender : IItemSender
        {
            public List<uint> Used { get; } = new List<uint>();

            public void SendUse(Serial serial) => Used.Add(serial.Value);
        }
    }
}
