using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>0xBC Season Change: сезон. Сервер: OutgoingPlayerPackets.cs, SendSeasonChange — byte сезон, byte «со звуком».</summary>
    public sealed class SeasonChangeHandler
    {
        public const byte PacketId = 0xBC;

        private readonly WorldEnvironment _environment;

        public SeasonChangeHandler(WorldEnvironment environment)
        {
            _environment = environment;
        }

        public void Handle(ref PacketReader reader) => _environment.SetSeason(reader.ReadByte());
    }
}
