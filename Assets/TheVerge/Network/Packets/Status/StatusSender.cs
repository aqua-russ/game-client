using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>Порт <see cref="IStatusSender"/> (ADR 0008 п. 1): запросы статуса в исходящие тика.</summary>
    public sealed class StatusSender : IStatusSender
    {
        private readonly NetworkSession _session;

        public StatusSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendStatusQuery(Serial serial)
        {
            var length = PacketTable.Get(PacketDirection.ClientToServer, StatusQueryPacket.PacketId).FixedLength;
            var writer = new PacketWriter(_session.Outgoing.Reserve(length));
            StatusQueryPacket.Write(ref writer, serial);
            _session.Outgoing.Commit(writer.Position);
        }
    }
}
