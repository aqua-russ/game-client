using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Combat;
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
    /// Действия бота для смерти и воскрешения (PLAN Стадия 4, п. 9): dead — ждать своей смерти по серверу (статус 0x2C,
    /// тело призрака, свой труп с вещами по слоям 0x89); alive — ждать, что сервер снова показывает живое тело и здоровье;
    /// deathof:0xТЕЛО — увидеть смерть другого (0xAF с трупом, труп в мире, вещи по слоям); target:mob:0xТЕЛО — прицел
    /// в ближайшего другого моба с этим телом (для [kill и [res).
    /// </summary>
    public sealed class BotDeathTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Other = new Serial(0x50);
        private static readonly Serial Corpse = new Serial(0x40000037);
        private static readonly Serial Hair = new Serial(0x7FFFFFFE);
        private static readonly Point3D Here = new Point3D(10, 10, 0);

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private CombatState _combat = null!;
        private CombatLog _journal = null!;
        private BotWorld _world = null!;
        private RecordingTargetSender _targets = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            var store = new EntityStore();
            var sent = new Recorder();
            _targets = new RecordingTargetSender();
            _combat = new CombatState();
            _journal = new CombatLog(_clock);
            _world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store), new ItemCommands(store, sent, _clock),
                new SpeechCommands(sent, _clock), new TargetCursor(_targets), new BotStatus(new MobileStatusUpdates(store), new StatusCommands(sent)))
            {
                Combat = new BotCombat(_combat, new CombatCommands(sent, _combat, store), _journal),
                Corpses = new CorpseEquipment(store),
            };
            store.Reset(new PlayerState(Player, 0x190, Here, Direction.North, new MapSize(7168, 4096)));
        }

        [TestCase("dead", true)]
        [TestCase("alive", true)]
        [TestCase("deathof:0x0190", true)]
        [TestCase("deathof:x", false)]
        [TestCase("target:mob:0x0190", true)]
        [TestCase("target:mob:x", false)]
        public void Parse(string text, bool valid)
        {
            Assert.That(BotAction.Parse(text, out _) != null, Is.EqualTo(valid));
        }

        [Test]
        public void Dead_WaitsForStatusGhostAndOwnCorpse()
        {
            var script = Script("dead");
            script.Step();

            _combat.OnDeathStatus();
            script.Step();
            Assert.That(script.IsDone, Is.False, "тело ещё живое");

            _world.Entities.UpdateMobile(Player, new MobileInfo(0x192, Here, Direction.North, 0, 0, 1));
            script.Step();
            Assert.That(script.IsDone, Is.False, "трупа ещё нет");

            PlaceCorpse(0x190);
            _world.Corpses!.Set(Corpse, new[] { new CorpseItem(Layer.Hair, Hair) });
            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            var text = Text();
            Assert.That(text, Does.Contain("статус смерти (0x2C)"));
            Assert.That(text, Does.Contain("тело призрака 0x0192"));
            Assert.That(text, Does.Contain("свой труп 0x40000037 (тело 0x0190)"));
            Assert.That(text, Does.Contain("Hair → 0x7FFFFFFE"));
        }

        [Test]
        public void Dead_LongTimeout()
        {
            var script = Script("dead");
            script.Step();

            _clock.NowMs += BotScript.ActionTimeoutMs + 1;
            script.Step();
            Assert.That(script.Problem, Is.Empty);

            _clock.NowMs += BotScript.DeathTimeoutMs;
            script.Step();
            Assert.That(script.Problem, Does.Contain("0x2C"));
        }

        [Test]
        public void Alive_GhostThenBodyAndHits()
        {
            _world.Entities.UpdateMobile(Player, new MobileInfo(0x192, Here, Direction.North, 0, 0, 1));
            var script = Script("alive");
            script.Step();
            Assert.That(script.IsDone, Is.False);

            _world.Entities.UpdateMobile(Player, new MobileInfo(0x190, Here, Direction.North, 0, 0, 1));
            _world.Status!.Updates.OnVital(Player, VitalKind.Hits, new Vital(10, 67));
            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(Text(), Does.Contain("Жив: тело 0x0190, здоровье 10/67"));
        }

        [Test]
        public void DeathOf_DeathAnimationCorpseAndLayers()
        {
            _world.Entities.UpdateMobile(Other, new MobileInfo(0x190, new Point3D(11, 10, 0), Direction.North, 0, 0, 1));
            var script = Script("deathof:0x0190");
            script.Step();
            Assert.That(script.IsDone, Is.False);

            PlaceCorpse(0x190);
            _world.Corpses!.Set(Corpse, new[] { new CorpseItem(Layer.Hair, Hair) });
            _journal.OnDeath(Other, Corpse);
            _world.Entities.UpdateMobile(Other, new MobileInfo(0x192, new Point3D(11, 10, 0), Direction.North, 0, 0, 1));
            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            var text = Text();
            Assert.That(text, Does.Contain("Смерть 0x00000050: анимация смерти (0xAF), труп 0x40000037 (тело 0x0190)"));
            Assert.That(text, Does.Contain("Hair → 0x7FFFFFFE"));
            Assert.That(text, Does.Contain("стал призраком (тело 0x0192)"));
        }

        [Test]
        public void DeathOf_BodyAlreadyChanged_TakesLastDeathFromJournal()
        {
            _world.Entities.UpdateMobile(Other, new MobileInfo(0x192, new Point3D(11, 10, 0), Direction.North, 0, 0, 1));
            PlaceCorpse(0x190);
            _journal.OnDeath(Other, Corpse);
            var script = Script("deathof:0x0190");

            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(Text(), Does.Contain("Смерть 0x00000050: анимация смерти (0xAF)"));
            Assert.That(Text(), Does.Contain("вещи по слоям (0x89): не приходили"));
        }

        [Test]
        public void DeathOf_NoSuchMobile_Problem()
        {
            var script = Script("deathof:0x00EE");

            script.Step();

            Assert.That(script.Problem, Does.Contain("0x00EE"));
        }

        [Test]
        public void TargetMob_NearestOtherWithBody()
        {
            _world.Entities.UpdateMobile(new Serial(0x60), new MobileInfo(0x190, new Point3D(15, 10, 0), Direction.North, 0, 0, 1));
            _world.Entities.UpdateMobile(Other, new MobileInfo(0x190, new Point3D(11, 10, 0), Direction.North, 0, 0, 1));
            var script = Script("target:mob:0x0190");
            _world.Target.OnRequest(false, 7, 0);

            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            Assert.That(_targets.Objects, Is.EqualTo(new[] { Other }));
        }

        private void PlaceCorpse(ushort body) =>
            _world.Entities.PlaceInWorld(Corpse, new ItemInfo(0x2006, body, Here, 0, 0, 0));

        private string Text() => string.Join("\n", _log.Entries.Select(e => e.Message));

        private BotScript Script(params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), _world, _log, _clock);

        private sealed class RecordingTargetSender : ITargetSender
        {
            public List<Serial> Objects { get; } = new List<Serial>();

            public void SendTarget(in TargetResponse response) => Objects.Add(response.Serial);
        }

        private sealed class Recorder : IItemSender, ISpeechSender, IStatusSender, ICombatSender
        {
            public void SendUse(Serial serial)
            {
            }

            public void SendSpeech(string text, ushort hue)
            {
            }

            public void SendStatusQuery(Serial serial)
            {
            }

            public void SendWarMode(bool warMode)
            {
            }

            public void SendAttack(Serial target)
            {
            }
        }
    }
}
