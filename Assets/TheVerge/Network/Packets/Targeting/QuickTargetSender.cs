using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Targeting
{
    /// <summary>Порт <see cref="IQuickTargetSender"/> (ADR 0008 п. 1): быстрые цели в исходящие тика.</summary>
    public sealed class QuickTargetSender : IQuickTargetSender
    {
        private readonly NetworkSession _session;

        public QuickTargetSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendBandageTarget(Serial bandage, Serial target)
        {
            var writer = new PacketWriter(_session.Outgoing.Reserve(QuickTargetPackets.BandageLength));
            QuickTargetPackets.WriteBandage(ref writer, bandage, target);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendTargetedSpell(int spellNumber, Serial target) =>
            SendNumber(QuickTargetPackets.SpellSubCommand, spellNumber, target);

        public void SendTargetedSkill(int skillId, Serial target) =>
            SendNumber(QuickTargetPackets.SkillSubCommand, skillId, target);

        public void SendTargetByResource(Serial item, int resourceType)
        {
            var writer = new PacketWriter(_session.Outgoing.Reserve(QuickTargetPackets.ShortAndSerialLength));
            QuickTargetPackets.WriteResource(ref writer, item, resourceType);
            _session.Outgoing.Commit(writer.Position);
        }

        private void SendNumber(ushort subCommand, int number, Serial target)
        {
            var writer = new PacketWriter(_session.Outgoing.Reserve(QuickTargetPackets.ShortAndSerialLength));
            QuickTargetPackets.WriteNumberAndTarget(ref writer, subCommand, number, target);
            _session.Outgoing.Commit(writer.Position);
        }
    }
}
