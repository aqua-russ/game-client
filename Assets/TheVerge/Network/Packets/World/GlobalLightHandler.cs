using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>0x4F Global Light: общий свет. Сервер: OutgoingLightPackets.cs, SendGlobalLightLevel — byte уровень.</summary>
    public sealed class GlobalLightHandler
    {
        public const byte PacketId = 0x4F;

        private readonly WorldEnvironment _environment;

        public GlobalLightHandler(WorldEnvironment environment)
        {
            _environment = environment;
        }

        public void Handle(ref PacketReader reader) => _environment.SetGlobalLight(reader.ReadByte());
    }
}
