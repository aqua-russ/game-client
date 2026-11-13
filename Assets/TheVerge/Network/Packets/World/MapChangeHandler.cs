using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0xBF 0x08 Map Change: номер карты. Сервер: Projects/Server/Network/Packets/OutgoingMapPackets.cs, SendMapChange —
    /// byte карта. При входе приходит дважды; при настоящей смене мир очищается (<see cref="EntityStore.ChangeMap"/>).
    /// </summary>
    public sealed class MapChangeHandler
    {
        public const byte PacketId = 0xBF;
        public const ushort SubCommand = 0x08;

        private readonly EntityStore _store;

        public MapChangeHandler(EntityStore store)
        {
            _store = store;
        }

        public void Handle(ref PacketReader reader) => _store.ChangeMap(reader.ReadByte());
    }
}
