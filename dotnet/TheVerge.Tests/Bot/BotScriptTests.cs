using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
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
        private BotWorld _world = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _sender = new RecordingItemSender();
            var store = new EntityStore();
            _world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store), new ItemCommands(store, _sender, _clock));
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

        private BotScript Script(params string[] actions)
        {
            var parsed = actions.Select(a => BotAction.Parse(a, out _)!).ToList();
            return new BotScript(parsed, _world, _log, _clock);
        }

        private string Text() => string.Join("\n", _log.Entries.Select(e => e.Message));

        private sealed class RecordingItemSender : IItemSender
        {
            public List<uint> Used { get; } = new List<uint>();

            public void SendUse(Serial serial) => Used.Add(serial.Value);
        }
    }
}
