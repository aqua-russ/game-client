using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>0x65 Weather: погода. Сервер: OutgoingPlayerPackets.cs, SendWeather — byte тип, плотность, температура.</summary>
    public sealed class WeatherHandler
    {
        public const byte PacketId = 0x65;

        private readonly WorldEnvironment _environment;

        public WeatherHandler(WorldEnvironment environment)
        {
            _environment = environment;
        }

        public void Handle(ref PacketReader reader)
        {
            var type = reader.ReadByte();
            var density = reader.ReadByte();
            var temperature = reader.ReadSByte();
            _environment.SetWeather(type, density, temperature);
        }
    }
}
