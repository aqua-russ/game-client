using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>0x6D Play Music: музыка. Сервер: OutgoingPlayerPackets.cs, SendPlayMusic — short номер; 0x1FFF — стоп.</summary>
    public sealed class PlayMusicHandler
    {
        public const byte PacketId = 0x6D;

        private readonly WorldEnvironment _environment;

        public PlayMusicHandler(WorldEnvironment environment)
        {
            _environment = environment;
        }

        public void Handle(ref PacketReader reader) => _environment.SetMusic(reader.ReadUInt16());
    }
}
