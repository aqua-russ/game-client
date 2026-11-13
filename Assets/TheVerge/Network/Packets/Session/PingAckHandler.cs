using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0x73 Ping Ack: эхо ping. Сервер: Projects/Server/Network/Packets/OutgoingPlayerPackets.cs, SendPingAck — номер,
    /// который прислал клиент.
    /// </summary>
    public sealed class PingAckHandler
    {
        public const byte PacketId = 0x73;

        private readonly GameSession _session;

        public PingAckHandler(GameSession session)
        {
            _session = session;
        }

        public void Handle(ref PacketReader reader) => _session.OnPingAck(reader.ReadByte());
    }
}
