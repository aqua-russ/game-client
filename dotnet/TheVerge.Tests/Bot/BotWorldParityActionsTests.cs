using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Targeting;
using TheVerge.Core.World;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действия бота для проверки мира в пяти точках (PLAN Стадия 2, п. 12): onmap:N — ждать карту N (после [go Trammel;
    /// карту сообщает сервер, 0xBF 0x08), seenmulti: — ждать дом или корабль (0xF3, тип multi) в пределах обзора multi —
    /// BaseMulti.GetUpdateRange сервера, <see cref="ServerLimits.MultiUpdateRange"/>.
    /// </summary>
    public sealed class BotWorldParityActionsTests
    {
        private static readonly Point3D Here = new Point3D(100, 200, 0);

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private BotWorld _world = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            var store = new EntityStore();
            _world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store), new ItemCommands(store, new NoItemSender(), _clock),
                new SpeechCommands(new NoSpeechSender(), _clock), new TargetCursor(new NoTargetSender()));
            store.Reset(new PlayerState(new Serial(0x3E), 0x190, Here, Direction.North, new MapSize(7168, 4096)));
        }

        [TestCase("onmap:0")]
        [TestCase("onmap:5")]
        public void OnMap_Parse_MapNumberFromZeroToFive(string text)
        {
            var action = BotAction.Parse(text, out var error);

            Assert.That(action, Is.Not.Null, error);
            Assert.That(action!.Kind, Is.EqualTo(BotActionKind.OnMap));
        }

        [TestCase("onmap:")]
        [TestCase("onmap:6")]
        [TestCase("onmap:-1")]
        [TestCase("onmap:abc")]
        public void OnMap_Parse_BadNumber_Rejected(string text)
        {
            Assert.That(BotAction.Parse(text, out var error), Is.Null);
            Assert.That(error, Does.Contain(text));
        }

        [Test]
        public void OnMap_AlreadyOnThatMap_DoneAtOnce()
        {
            _world.Entities.ChangeMap(0);
            var script = Script("onmap:0");

            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(Text(), Does.Contain("карте 0"));
        }

        [Test]
        public void OnMap_WaitsUntilServerChangesMap()
        {
            _world.Entities.ChangeMap(0);
            var script = Script("onmap:1");

            script.Step();
            Assert.That(script.IsDone, Is.False);

            _world.Entities.ChangeMap(1);
            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
        }

        [Test]
        public void OnMap_NoChange_ProblemNamesWantedAndCurrentMap()
        {
            _world.Entities.ChangeMap(0);
            var script = Script("onmap:1");
            script.Step();
            _clock.NowMs += BotScript.ActionTimeoutMs;

            script.Step();

            Assert.That(script.Problem, Does.Contain("onmap:1").And.Contain("карте 1").And.Contain("карте 0"));
        }

        [Test]
        public void SeenMulti_Parse_NoArgumentOnly()
        {
            Assert.That(BotAction.Parse("seenmulti:", out _)!.Kind, Is.EqualTo(BotActionKind.SeenMulti));
            Assert.That(BotAction.Parse("seenmulti:0x4000", out var error), Is.Null);
            Assert.That(error, Does.Contain("seenmulti:0x4000"));
        }

        [Test]
        public void SeenMulti_PlainItemNearby_DoesNotCount()
        {
            _world.Entities.PlaceInWorld(new Serial(0x40000001), new ItemInfo(0x09D0, 1, new Point3D(101, 200, 0), 0, 0, 0));
            var script = Script("seenmulti:");

            script.Step();

            Assert.That(script.IsDone, Is.False);
        }

        [Test]
        public void SeenMulti_MultiBeyondItsRange_DoesNotCount()
        {
            _world.Entities.PlaceInWorld(new Serial(0x40000002), new ItemInfo(0x4000, 1, new Point3D((short)(Here.X + ServerLimits.MultiUpdateRange + 1), Here.Y, 0), 0, 0, 0, true));
            var script = Script("seenmulti:");

            script.Step();

            Assert.That(script.IsDone, Is.False);
        }

        [Test]
        public void SeenMulti_MultiAtEdgeOfItsRange_Counts()
        {
            _world.Entities.PlaceInWorld(new Serial(0x40000002), new ItemInfo(0x4000, 1, new Point3D(Here.X, (short)(Here.Y - ServerLimits.MultiUpdateRange), 0), 0, 0, 0, true));
            var script = Script("seenmulti:");

            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(Text(), Does.Contain("0x40000002"));
        }

        [Test]
        public void SeenMulti_Nothing_ProblemSaysSo()
        {
            var script = Script("seenmulti:");
            script.Step();
            _clock.NowMs += BotScript.ActionTimeoutMs;

            script.Step();

            Assert.That(script.Problem, Does.Contain("seenmulti:").And.Contain("дом или корабль"));
        }

        private BotScript Script(params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), _world, _log, _clock);

        private string Text() => string.Join("\n", _log.Entries.Select(e => e.Message));

        private sealed class NoTargetSender : ITargetSender
        {
            public void SendTarget(in TargetResponse response)
            {
            }
        }

        private sealed class NoSpeechSender : ISpeechSender
        {
            public void SendSpeech(string text, ushort hue)
            {
            }
        }

        private sealed class NoItemSender : IItemSender
        {
            public void SendUse(Serial serial)
            {
            }
        }
    }
}
