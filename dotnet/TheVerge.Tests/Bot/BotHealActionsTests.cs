using System.Collections.Generic;
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
    /// Действия бота для проверки прицела Стадии 4 (п. 6): bandage:self — быстрый бинт (0xBF 0x2C) бинтом не дальше 2
    /// клеток (Bandage.Range сервера); use:0xГРАФИКА — двойной клик по предмету рядом (сервер ответит курсором);
    /// seen:0xГРАФИКА,КОЛИЧЕСТВО — ждать стопку под ногами с этим количеством (бинт при начале лечения тратится).
    /// </summary>
    public sealed class BotHealActionsTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Bandages = new Serial(0x40000700);

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private Recorder _sent = null!;
        private BotWorld _world = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _sent = new Recorder();
            var store = new EntityStore();
            var actions = new ActionTimer();
            _world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store), new ItemCommands(store, _sent, _clock, actions),
                new SpeechCommands(_sent, _clock), new TargetCursor(_sent))
            {
                QuickTargets = new TargetCommands(_sent, _clock, actions),
            };
            store.Reset(new PlayerState(Player, 0x190, new Point3D(10, 10, 0), Direction.North, new MapSize(7168, 4096)));
        }

        [TestCase("bandage:self", true)]
        [TestCase("bandage:friend", false)]
        [TestCase("use:0x0E21", true)]
        [TestCase("seen:0x0E21,4", true)]
        [TestCase("seen:0x0E21,x", false)]
        public void Parse(string text, bool valid)
        {
            Assert.That(BotAction.Parse(text, out _) != null, Is.EqualTo(valid));
        }

        [Test]
        public void BandageSelf_QuickBandageWithBandageNearby()
        {
            Place(new Point3D(12, 9, 0), 5);
            var script = Script("bandage:self");

            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(_sent.Quick, Is.EqualTo(new[] { "bandage " + Bandages.Value.ToString("X8") + " " + Player.Value.ToString("X8") }));
        }

        [Test]
        public void BandageSelf_NoBandageNearby_Problem()
        {
            Place(new Point3D(13, 10, 0), 5);
            var script = Script("bandage:self");

            script.Step();

            Assert.That(script.Problem, Does.Contain("бинт"));
            Assert.That(_sent.Quick, Is.Empty);
        }

        [Test]
        public void UseGraphic_DoubleClicksItemNearby()
        {
            Place(new Point3D(10, 10, 0), 5);
            var script = Script("use:0x0E21");

            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(_sent.Used, Is.EqualTo(new[] { Bandages.Value }));
        }

        [Test]
        public void SeenWithAmount_WaitsForAmount()
        {
            Place(new Point3D(10, 10, 0), 5);
            var script = Script("seen:0x0E21,4");

            script.Step();
            Assert.That(script.IsDone, Is.False);

            Place(new Point3D(10, 10, 0), 4);
            script.Step();
            Assert.That(script.IsDone, Is.True);
        }

        [Test]
        public void Target_LogsCursorKind()
        {
            var script = Script("target:self");
            _world.Target.OnRequest(false, 9, TargetCursor.FlagBeneficial);

            script.Step();

            Assert.That(string.Join("\n", _log.Entries.Select(e => e.Message)), Does.Contain("польза"));
        }

        private void Place(Point3D position, ushort amount) =>
            _world.Entities.PlaceInWorld(Bandages, new ItemInfo(0x0E21, amount, position, 0, 0, 0));

        private BotScript Script(params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), _world, _log, _clock);

        private sealed class Recorder : IItemSender, ISpeechSender, ITargetSender, IQuickTargetSender
        {
            public List<uint> Used { get; } = new List<uint>();
            public List<string> Quick { get; } = new List<string>();

            public void SendUse(Serial serial) => Used.Add(serial.Value);

            public void SendSpeech(string text, ushort hue)
            {
            }

            public void SendTarget(in TargetResponse response)
            {
            }

            public void SendBandageTarget(Serial bandage, Serial target) => Quick.Add("bandage " + bandage.Value.ToString("X8") + " " + target.Value.ToString("X8"));

            public void SendTargetedSpell(int spellNumber, Serial target) => Quick.Add("spell " + spellNumber);

            public void SendTargetedSkill(int skillId, Serial target) => Quick.Add("skill " + skillId);

            public void SendTargetByResource(Serial item, int resourceType) => Quick.Add("resource " + resourceType);
        }
    }
}
