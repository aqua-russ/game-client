using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x8C Server Redirect: адрес игрового сервера и ключ. Сервер: Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs, SendPlayServerAck — WriteLE(RawAddress), то есть байты IPv4 в обычном порядке
    /// (в отличие от 0xA8), затем short порт и int ключ big-endian.
    /// </summary>
    public sealed class ServerRedirectHandler
    {
        public const byte PacketId = 0x8C;

        private readonly LoginFlow _flow;

        public ServerRedirectHandler(LoginFlow flow)
        {
            _flow = flow;
        }

        public void Handle(ref PacketReader reader)
        {
            var host = IPv4Text.Format(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            var port = reader.ReadUInt16(); // сервер пишет (short)port, порт выше 32767 — тоже порт
            var authKey = reader.ReadInt32();
            _flow.OnRedirect(new ServerRedirect(host, port, authKey));
        }
    }
}
