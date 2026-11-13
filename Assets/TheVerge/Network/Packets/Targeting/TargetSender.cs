using TheVerge.Core.Ports;
using TheVerge.Core.Targeting;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Targeting
{
    /// <summary>Порт <see cref="ITargetSender"/> (ADR 0008 п. 1): ответ на прицел в исходящие тика.</summary>
    public sealed class TargetSender : ITargetSender
    {
        private readonly NetworkSession _session;

        public TargetSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendTarget(in TargetResponse response)
        {
            var length = PacketTable.Get(PacketDirection.ClientToServer, TargetResponsePacket.PacketId).FixedLength;
            var writer = new PacketWriter(_session.Outgoing.Reserve(length));
            TargetResponsePacket.Write(ref writer, response);
            _session.Outgoing.Commit(writer.Position);
        }
    }
}
