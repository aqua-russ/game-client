using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x82 Login Rejected: логин-сервер отказал. Сервер: Projects/Server/Network/Packets/OutgoingAccountPackets.cs,
    /// SendAccountLoginRejected — номер и байт причины (ALRReason); затем сервер закрывает соединение.
    /// </summary>
    public sealed class LoginRejectedHandler
    {
        public const byte PacketId = 0x82;

        private readonly LoginFlow _flow;

        public LoginRejectedHandler(LoginFlow flow)
        {
            _flow = flow;
        }

        public void Handle(ref PacketReader reader) => _flow.OnLoginRejected((LoginRejectReason)reader.ReadByte());
    }
}
