using TheVerge.Core.Movement;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0xBF 0x08 Map Change: номер карты. Сервер: Projects/Server/Network/Packets/OutgoingMapPackets.cs, SendMapChange —
    /// byte карта. При входе приходит дважды; при настоящей смене мир очищается (<see cref="EntityStore.ChangeMap"/>), а
    /// сервер сбрасывает счёт шагов и шлёт новое место своего персонажа следом в 0x20 (Mobile.InternalMapChange).
    /// </summary>
    public sealed class MapChangeHandler
    {
        public const byte PacketId = 0xBF;
        public const ushort SubCommand = 0x08;

        private readonly EntityStore _store;
        private readonly MovementReplies? _movement;

        public MapChangeHandler(EntityStore store, MovementReplies? movement = null)
        {
            _store = store;
            _movement = movement;
        }

        public void Handle(ref PacketReader reader)
        {
            var map = reader.ReadByte();
            var known = _store.MapIndex;
            _store.ChangeMap(map);
            if (known != EntityStore.UnknownMap && known != map)
            {
                _movement?.OnMapChanged();
            }
        }
    }
}
