using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.Targeting;
using TheVerge.Core.World;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действия бота для сверки с сервером (PLAN Стадия 2, п. 11): target:0xГРАФИКА — прицел в предмет под ногами (для
    /// [remove), dump:&lt;папка&gt; — [DumpVisible, свежий файл сервера, сверка со своим снимком.
    /// </summary>
    public sealed class BotParityActionsTests
    {
        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private RecordingSpeechSender _speech = null!;
        private RecordingTargetSender _targets = null!;
        private BotWorld _world = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _speech = new RecordingSpeechSender();
            _targets = new RecordingTargetSender();
            var store = new EntityStore();
            _world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store), new ItemCommands(store, new NoItemSender(), _clock),
                new SpeechCommands(_speech, _clock), new TargetCursor(_targets));
            store.Reset(new PlayerState(new Serial(0x3E), 0x190, new Point3D(1, 1, 0), Direction.North, new MapSize(7168, 4096)));
        }

        [Test]
        public void TargetGraphic_TargetsItemAtFeet()
        {
            _world.Entities.PlaceInWorld(new Serial(0x40000701), new ItemInfo(0x09D0, 1, new Point3D(1, 1, 0), 0, 0, 0));
            var script = Script("target:0x09D0");
            _world.Target.OnRequest(false, 5, 0);

            script.Step();

            Assert.That(_targets.Responses.Single().Serial.Value, Is.EqualTo(0x40000701u));
        }

        [Test]
        public void TargetGraphic_NothingAtFeet_Problem()
        {
            var script = Script("target:0x09D0");
            _world.Target.OnRequest(false, 5, 0);

            script.Step();

            Assert.That(script.Problem, Does.Contain("0x09D0"));
        }

        [Test]
        public void Dump_SaysCommand_WaitsForFreshFile_ComparesEqual()
        {
            var dir = TempDir();
            var serverFile = System.IO.Path.Combine(dir, "visible-0000003E.json");
            System.IO.File.WriteAllText(serverFile, "старый файл прошлого запуска");
            System.IO.File.SetLastWriteTimeUtc(serverFile, System.DateTime.UtcNow.AddMinutes(-5));
            var script = Script("dump:" + dir);

            script.Step();
            Assert.That(_speech.Sent, Is.EqualTo(new[] { "[DumpVisible" }));
            _clock.NowMs += BotScript.DumpSettleMs;
            script.Step();
            Assert.That(script.IsDone, Is.False, "файл сервера старый — ждём новый");

            System.IO.File.WriteAllText(serverFile, WorldSnapshot.Write(WorldSnapshot.FromStore(_world.Entities)));
            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(Text(), Does.Contain("Сверка с сервером: совпадает объектов 1."));
            Assert.That(System.IO.File.Exists(serverFile + ".bot.json"), Is.True, "снимок бота рядом — для разбора");
        }

        [Test]
        public void Dump_Differences_ProblemNamesThem()
        {
            var dir = TempDir();
            var script = Script("dump:" + dir);
            script.Step();
            _clock.NowMs += BotScript.DumpSettleMs;
            var server = WorldSnapshot.FromStore(_world.Entities);
            server.Objects[0].Hue = 0x21;
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "visible-0000003E.json"), WorldSnapshot.Write(server));

            script.Step();

            Assert.That(script.Problem, Does.Contain("hue").And.Contain("0x0000003E"));
        }

        [Test]
        public void MapDump_WithoutData_ProblemAtOnce_NothingSaid()
        {
            var script = Script("mapdump:100,1," + TempDir());

            script.Step();

            Assert.That(script.Problem, Does.Contain("--data"));
            Assert.That(_speech.Sent, Is.Empty);
        }

        [Test]
        public void StatusDump_WithoutStatus_ProblemAtOnce()
        {
            var script = Script("statusdump:" + TempDir());

            script.Step();

            Assert.That(script.Problem, Does.Contain("состояни"));
            Assert.That(_speech.Sent, Is.Empty);
        }

        [Test]
        public void StatusDump_AsksStatusOfEveryMobile_ThenSaysCommandAfterPause()
        {
            var sender = new RecordingStatusSender();
            var store = _world.Entities;
            store.UpdateMobile(new Serial(0x100), new MobileInfo(0xEE, new Point3D(2, 2, 0), Direction.North, 0, 0, 3));
            var status = new BotStatus(new MobileStatusUpdates(store), new StatusCommands(sender));
            var world = new BotWorld(store, _world.Environment, _world.Containers, _world.Items, _world.Speech, _world.Target, status);
            var script = new BotScript(new[] { BotAction.Parse("statusdump:" + TempDir(), out _)! }, world, _log, _clock);

            script.Step();
            Assert.That(sender.Queried, Is.EquivalentTo(new uint[] { 0x3E, 0x100 }));
            Assert.That(_speech.Sent, Is.Empty, "сначала ответы на запросы статуса");

            _clock.NowMs += BotScript.DumpSettleMs;
            script.Step();
            _world.Speech.Update(_clock.NowMs);
            Assert.That(_speech.Sent, Is.EqualTo(new[] { "[DumpMobile" }));
        }

        [Test]
        public void SeenMobile_WaitsForBodyNearby()
        {
            var script = Script("seenmob:0x00EE");

            script.Step();
            Assert.That(script.IsDone, Is.False);

            _world.Entities.UpdateMobile(new Serial(0x100), new MobileInfo(0xEE, new Point3D(3, 4, 0), Direction.North, 0, 0, 3));
            script.Step();

            Assert.That(script.IsDone, Is.True);
            Assert.That(Text(), Does.Contain("0x00EE").And.Contain("0x00000100"));
        }

        [Test]
        public void SeenMobile_OwnBodyDoesNotCount()
        {
            var script = Script("seenmob:0x0190");

            script.Step();

            Assert.That(script.IsDone, Is.False);
        }

        private static string TempDir()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "verge-dump-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            return dir;
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

        private sealed class RecordingStatusSender : IStatusSender
        {
            public List<uint> Queried { get; } = new List<uint>();

            public void SendStatusQuery(Serial serial) => Queried.Add(serial.Value);
        }

        private sealed class NoItemSender : IItemSender
        {
            public void SendUse(Serial serial)
            {
            }
        }
    }
}
