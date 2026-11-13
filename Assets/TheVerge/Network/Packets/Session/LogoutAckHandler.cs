using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0xD1 Logout Ack: сервер разрешил выход. Сервер: Projects/Server/Network/Packets/OutgoingPlayerPackets.cs,
    /// SendLogoutAck — 0xD1 0x01. Дальше соединение закрывает клиент.
    /// </summary>
    public sealed class LogoutAckHandler
    {
        public const byte PacketId = 0xD1;

        private readonly GameSession _session;

        public LogoutAckHandler(GameSession session)
        {
            _session = session;
        }

        public void Handle(ref PacketReader reader) => _session.OnLogoutAck();
    }
}
