using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>Порт <see cref="IItemSender"/> (ADR 0008 п. 1): действия с предметами в исходящие тика.</summary>
    public sealed class ItemSender : IItemSender
    {
        private readonly NetworkSession _session;

        public ItemSender(NetworkSession session)
        {
            _session = session;
        }

        public void SendUse(Serial serial)
        {
            var length = PacketTable.Get(PacketDirection.ClientToServer, DoubleClickPacket.PacketId).FixedLength;
            var writer = new PacketWriter(_session.Outgoing.Reserve(length));
            DoubleClickPacket.Write(ref writer, serial);
            _session.Outgoing.Commit(writer.Position);
        }
    }
}
