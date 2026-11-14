using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Items;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Targeting;
using TheVerge.Core.World;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Targeting
{
    /// <summary>
    /// Быстрые цели без курсора (PLAN Стадия 4, п. 6) — 0xBF 0x2C, 0x2D, 0x2E, 0x30, как их читает сервер
    /// (IncomingExtendedCommandPackets.cs): бинт — серийник бинта (предмет) и цели (моб), не чаще задержки действий,
    /// общей с двойным кликом; заклинание и навык — номер и цель; по ресурсу — только предмет (иначе сервер пропускает).
    /// </summary>
    public sealed class TargetCommandsTests
    {
        private static readonly Serial Bandage = new Serial(0x40000010);
        private static readonly Serial Self = new Serial(0x3E);

        private FakeClock _clock = null!;
        private RecordingQuickSender _sender = null!;
        private ActionTimer _actions = null!;
        private TargetCommands _commands = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { NowMs = 10_000 };
            _sender = new RecordingQuickSender();
            _actions = new ActionTimer();
            _commands = new TargetCommands(_sender, _clock, _actions);
        }

        [Test]
        public void Bandage_SendsBandageAndTarget()
        {
            Assert.That(_commands.Bandage(Bandage, Self), Is.True);

            Assert.That(_sender.Sent, Is.EqualTo(new[] { "bandage 40000010 0000003E" }));
        }

        [Test]
        public void Bandage_WrongSerialKinds_NothingSent()
        {
            Assert.That(_commands.Bandage(Self, Self), Is.False, "бинт — предмет");
            Assert.That(_commands.Bandage(Bandage, Bandage), Is.False, "цель — моб");
            Assert.That(_sender.Sent, Is.Empty);
        }

        [Test]
        public void Bandage_SharesDelayWithDoubleClick()
        {
            var items = new ItemCommands(new EntityStore(), new NoItemSender(), _clock, _actions);
            items.Use(Bandage);

            Assert.That(_commands.Bandage(Bandage, Self), Is.False);

            _clock.NowMs += ServerLimits.ActionDelayMs;
            Assert.That(_commands.Bandage(Bandage, Self), Is.True);
        }

        [Test]
        public void CastSkillResource_SentAsIs()
        {
            Assert.That(_commands.CastAt(29, Self), Is.True);
            Assert.That(_commands.UseSkillAt(1, Bandage), Is.True);
            Assert.That(_commands.TargetByResource(Bandage, 2), Is.True);

            Assert.That(_sender.Sent, Is.EqualTo(new[] { "spell 29 0000003E", "skill 1 40000010", "resource 40000010 2" }));
        }

        [Test]
        public void BadArguments_NothingSent()
        {
            Assert.That(_commands.CastAt(0, Self), Is.False, "номер заклинания начинается с 1");
            Assert.That(_commands.CastAt(5, Serial.Zero), Is.False);
            Assert.That(_commands.UseSkillAt(-1, Self), Is.False);
            Assert.That(_commands.TargetByResource(Self, 1), Is.False, "по ресурсу — только предмет");
            Assert.That(_sender.Sent, Is.Empty);
        }

        private sealed class RecordingQuickSender : IQuickTargetSender
        {
            public List<string> Sent { get; } = new List<string>();

            public void SendBandageTarget(Serial bandage, Serial target) => Sent.Add("bandage " + bandage.Value.ToString("X8") + " " + target.Value.ToString("X8"));

            public void SendTargetedSpell(int spellNumber, Serial target) => Sent.Add("spell " + spellNumber + " " + target.Value.ToString("X8"));

            public void SendTargetedSkill(int skillId, Serial target) => Sent.Add("skill " + skillId + " " + target.Value.ToString("X8"));

            public void SendTargetByResource(Serial item, int resourceType) => Sent.Add("resource " + item.Value.ToString("X8") + " " + resourceType);
        }

        private sealed class NoItemSender : IItemSender
        {
            public void SendUse(Serial serial)
            {
            }
        }
    }
}
