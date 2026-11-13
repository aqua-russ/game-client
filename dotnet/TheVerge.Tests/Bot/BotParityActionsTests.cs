using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
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

        private sealed class NoItemSender : IItemSender
        {
            public void SendUse(Serial serial)
            {
            }
        }
    }
}
