using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0x55 Login Complete: начальная загрузка мира окончена. Сервер: Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs, SendLoginComplete — только номер; шлёт его в конце DoLogin (IncomingAccountPackets.cs).
    /// </summary>
    public sealed class LoginCompleteHandler
    {
        public const byte PacketId = 0x55;

        private readonly WorldEntry _world;

        public LoginCompleteHandler(WorldEntry world)
        {
            _world = world;
        }

        public void Handle(ref PacketReader reader) => _world.OnLoginComplete();
    }
}
