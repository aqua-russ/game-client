using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>0x5B Current Time: время сервера. Сервер: OutgoingPlayerPackets.cs, SendCurrentTime — byte часы, минуты, секунды.</summary>
    public sealed class CurrentTimeHandler
    {
        public const byte PacketId = 0x5B;

        private readonly WorldEnvironment _environment;

        public CurrentTimeHandler(WorldEnvironment environment)
        {
            _environment = environment;
        }

        public void Handle(ref PacketReader reader)
        {
            var hour = reader.ReadByte();
            var minute = reader.ReadByte();
            var second = reader.ReadByte();
            _environment.SetTime(hour, minute, second);
        }
    }
}
