using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x4E Personal Light: свет своего персонажа (ночное зрение, факел). Сервер: OutgoingLightPackets.cs,
    /// SendPersonalLightLevel — Serial, byte уровень. Сервер шлёт его только о самом персонаже: чужой серийник пропускается.
    /// </summary>
    public sealed class PersonalLightHandler
    {
        public const byte PacketId = 0x4E;

        private readonly EntityStore _store;
        private readonly WorldEnvironment _environment;

        public PersonalLightHandler(EntityStore store, WorldEnvironment environment)
        {
            _store = store;
            _environment = environment;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var level = reader.ReadByte();
            if (serial == _store.PlayerSerial)
            {
                _environment.SetPersonalLight(level);
            }
        }
    }
}
