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
    /// fight:0xТЕЛО (PLAN Стадия 4, п. 7): бот просит боевую стойку, ждёт её от сервера (0x72), атакует моба с этим телом
    /// (0x05), ждёт, что сервер назовёт его противником (0xAA), и ждёт его смерти — моб исчезает из мира. Бой долгий —
    /// у шага свой срок (<see cref="BotScript.FightTimeoutMs"/>), а не общие 10 с.
    /// </summary>
    public sealed class BotFightTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Rat = new Serial(0x100);
        private static readonly Serial Corpse = new Serial(0x40000900);

        /// <summary>Труп: графика 0x2006, количество — тело (Corpse.cs: «protocol defines that for itemid 0x2006, amount=body»).</summary>
        private const ushort CorpseGraphic = 0x2006;

        private FakeClock _clock = null!;
        private RecordingLog _log = null!;
        private Recorder _sent = null!;
        private CombatState _combat = null!;
        private CombatLog _journal = null!;
        private BotWorld _world = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 1000 };
            _log = new RecordingLog();
            _sent = new Recorder();
            var store = new EntityStore();
            _combat = new CombatState();
            _journal = new CombatLog(_clock);
            _world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store), new ItemCommands(store, _sent, _clock),
                new SpeechCommands(_sent, _clock), new TargetCursor(_sent), new BotStatus(new MobileStatusUpdates(store), new StatusCommands(_sent)))
            {
                Combat = new BotCombat(_combat, new CombatCommands(_sent, _combat, store), _journal),
            };
            store.Reset(new PlayerState(Player, 0x190, new Point3D(10, 10, 0), Direction.North, new MapSize(7168, 4096)));
            store.UpdateMobile(Rat, new MobileInfo(0xEE, new Point3D(11, 10, 0), Direction.North, 0, 0, 3));
        }

        [Test]
        public void Fight_WarModeThenAttackThenWaitsForDeath()
        {
            var script = Script("fight:0x00EE");

            script.Step();
            Assert.That(_sent.Sent, Is.EqualTo(new[] { "стойка True" }));

            _combat.OnWarMode(true);
            script.Step();
            Assert.That(_sent.Sent.Last(), Is.EqualTo("атака 100"));

            _combat.OnCombatant(Rat);
            _world.Status!.Updates.OnVital(Rat, VitalKind.Hits, new Vital(40, 100));
            _world.Status.Updates.OnVital(Player, VitalKind.Hits, new Vital(48, 50));
            _journal.OnSwing(Player, Rat);
            _journal.OnAnimation(Player, 9);
            _clock.NowMs += 1500;
            _journal.OnAnimation(Rat, 5);
            _journal.OnDamage(Player, 2);
            _journal.OnSound(0x1A4, new Point3D(11, 10, 0));
            _journal.OnEffect(EffectKind.Moving, Player, Rat, 0x36D4, new Point3D(10, 10, 0), 0);
            script.Step();
            Assert.That(script.IsDone, Is.False);

            _world.Entities.Remove(Rat);
            script.Step();
            Assert.That(script.IsDone, Is.False, "моб пропал, а трупа ещё нет — мог просто уйти из виду");

            _world.Entities.PlaceInWorld(Corpse, new ItemInfo(CorpseGraphic, 0xEE, new Point3D(11, 10, 0), 0, 0, 0));
            script.Step();

            Assert.That(script.IsDone, Is.True, script.Problem);
            var text = string.Join("\n", _log.Entries.Select(e => e.Message));
            Assert.That(text, Does.Contain("Стойка: боевая"));
            Assert.That(text, Does.Contain("Противник по серверу: 0x00000100."));
            Assert.That(text, Does.Contain("Противник: здоровье 40/100"));
            Assert.That(text, Does.Contain("убит: труп 0x40000900"));
            Assert.That(text, Does.Contain("Бот получил урон: здоровье опускалось до 48/50"));
            Assert.That(text, Does.Contain("Журнал боя: +0.000 с замах 0x0000003E → 0x00000100"));
            Assert.That(text, Does.Contain("Журнал боя: +0.000 с анимация 0x0000003E действие 9"));
            Assert.That(text, Does.Contain("Журнал боя: +1.500 с анимация 0x00000100 действие 5"));
            Assert.That(text, Does.Contain("Журнал боя: +1.500 с урон 0x0000003E: 2"));
            Assert.That(text, Does.Contain("Журнал боя: +1.500 с звук 0x01A4 в (11, 10, 0)"));
            Assert.That(text, Does.Contain("Журнал боя: +1.500 с эффект Moving 0x36D4 0x0000003E → 0x00000100"));
            Assert.That(text, Does.Contain("замахов бота по противнику 1, анимаций бота 1, противника 1, урона 1, эффектов 1, звуков 1"));
        }

        [Test]
        public void Fight_PicksNearestMobileWithBody()
        {
            var far = new Serial(0x101);
            _world.Entities.UpdateMobile(far, new MobileInfo(0xEE, new Point3D(15, 10, 0), Direction.North, 0, 0, 3));
            _world.Entities.UpdateMobile(Rat, new MobileInfo(0xEE, new Point3D(10, 11, 0), Direction.North, 0, 0, 3));
            _combat.OnWarMode(true);
            var script = Script("fight:0x00EE");

            script.Step();

            Assert.That(_sent.Sent, Is.EqualTo(new[] { "атака 100" }));
        }

        [Test]
        public void Fight_CorpseOfOtherBody_NotDeath()
        {
            _combat.OnWarMode(true);
            var script = Script("fight:0x00EE");
            script.Step();
            _combat.OnCombatant(Rat);
            _world.Entities.Remove(Rat);

            _world.Entities.PlaceInWorld(Corpse, new ItemInfo(CorpseGraphic, 0x190, new Point3D(11, 10, 0), 0, 0, 0));
            script.Step();

            Assert.That(script.IsDone, Is.False);
        }

        [Test]
        public void Fight_AlreadyInWarMode_AttacksAtOnce()
        {
            _combat.OnWarMode(true);
            var script = Script("fight:0x00EE");

            script.Step();

            Assert.That(_sent.Sent, Is.EqualTo(new[] { "атака 100" }));
        }

        [Test]
        public void Fight_NoSuchMobile_Problem()
        {
            var script = Script("fight:0x0011");

            script.Step();

            Assert.That(script.Problem, Does.Contain("0x0011"));
        }

        [Test]
        public void Fight_LongTimeout()
        {
            var script = Script("fight:0x00EE");
            script.Step();

            _clock.NowMs += BotScript.ActionTimeoutMs + 1;
            script.Step();
            Assert.That(script.Problem, Is.Empty, "общих 10 с бою мало");

            _clock.NowMs += BotScript.FightTimeoutMs;
            script.Step();
            Assert.That(script.Problem, Does.Contain("стойк"));
        }

        private BotScript Script(params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), _world, _log, _clock);

        private sealed class Recorder : IItemSender, ISpeechSender, ITargetSender, IStatusSender, ICombatSender
        {
            public List<string> Sent { get; } = new List<string>();

            public void SendUse(Serial serial)
            {
            }

            public void SendSpeech(string text, ushort hue)
            {
            }

            public void SendTarget(in TargetResponse response)
            {
            }

            public void SendStatusQuery(Serial serial)
            {
            }

            public void SendWarMode(bool warMode) => Sent.Add("стойка " + warMode);

            public void SendAttack(Serial target) => Sent.Add("атака " + target.Value.ToString("X"));
        }
    }
}
