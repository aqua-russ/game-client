using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>Порт <see cref="ICombatSender"/> (ADR 0008 п. 1): стойка и атака в исходящие тика.</summary>
    public sealed class CombatSender : ICombatSender
    {
        private readonly NetworkSession _session;

        public CombatSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendWarMode(bool warMode)
        {
            var writer = new PacketWriter(_session.Outgoing.Reserve(CombatRequestPackets.Length));
            CombatRequestPackets.WriteWarMode(ref writer, warMode);
            _session.Outgoing.Commit(writer.Position);
        }

        public void SendAttack(Serial target)
        {
            var writer = new PacketWriter(_session.Outgoing.Reserve(CombatRequestPackets.Length));
            CombatRequestPackets.WriteAttack(ref writer, target);
            _session.Outgoing.Commit(writer.Position);
        }
    }
}
