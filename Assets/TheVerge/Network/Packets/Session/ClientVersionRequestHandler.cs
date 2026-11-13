using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0xBD Client Version Request: сервер спрашивает версию. Сервер: Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs, SendClientVersionRequest — только заголовок (0xBD 00 03). Ответ — в том же тике.
    /// </summary>
    public sealed class ClientVersionRequestHandler
    {
        public const byte PacketId = 0xBD;

        private readonly ServerRequests _requests;

        public ClientVersionRequestHandler(ServerRequests requests)
        {
            _requests = requests;
        }

        public void Handle(ref PacketReader reader) => _requests.OnClientVersionRequest();
    }
}
