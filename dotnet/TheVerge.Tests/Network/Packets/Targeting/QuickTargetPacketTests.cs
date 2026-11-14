using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Network.Packets.Targeting;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Targeting
{
    /// <summary>
    /// Быстрые цели 0xBF от клиента — как их читает сервер (Projects/UOContent/Network/Packets/IncomingExtendedCommandPackets.cs):
    /// 0x2C BandageTarget — uint бинт, uint цель; 0x2D TargetedSpell — short номер заклинания (с 1, сервер вычитает 1),
    /// uint цель; 0x2E TargetedSkillUse — short навык, uint цель; 0x30 TargetByResourceMacro — uint предмет, short ресурс.
    /// Перед ними — 0xBF, ushort длина, ushort подкоманда.
    /// </summary>
    public sealed class QuickTargetPacketTests
    {
        private NetworkSession _session = null!;
        private QuickTargetSender _sender = null!;

        [SetUp]
        public void SetUp()
        {
            _session = new NetworkSession(new PacketDispatcher(new RecordingLog()), new RecordingLog());
            _sender = new QuickTargetSender(_session);
        }

        [Test]
        public void Bandage_13Bytes()
        {
            _sender.SendBandageTarget(new Serial(0x40000010), new Serial(0x3E));

            Assert.That(_session.Outgoing.Pending.ToArray(),
                Is.EqualTo(new byte[] { 0xBF, 0x00, 0x0D, 0x00, 0x2C, 0x40, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x3E }));
        }

        [Test]
        public void Spell_11Bytes()
        {
            _sender.SendTargetedSpell(29, new Serial(0x3E));

            Assert.That(_session.Outgoing.Pending.ToArray(),
                Is.EqualTo(new byte[] { 0xBF, 0x00, 0x0B, 0x00, 0x2D, 0x00, 0x1D, 0x00, 0x00, 0x00, 0x3E }));
        }

        [Test]
        public void Skill_11Bytes()
        {
            _sender.SendTargetedSkill(1, new Serial(0x40000010));

            Assert.That(_session.Outgoing.Pending.ToArray(),
                Is.EqualTo(new byte[] { 0xBF, 0x00, 0x0B, 0x00, 0x2E, 0x00, 0x01, 0x40, 0x00, 0x00, 0x10 }));
        }

        [Test]
        public void Resource_11Bytes()
        {
            _sender.SendTargetByResource(new Serial(0x40000010), 2);

            Assert.That(_session.Outgoing.Pending.ToArray(),
                Is.EqualTo(new byte[] { 0xBF, 0x00, 0x0B, 0x00, 0x30, 0x40, 0x00, 0x00, 0x10, 0x00, 0x02 }));
        }
    }
}
