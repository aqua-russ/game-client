using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0xBF 0x18 Map Patches: сколько блоков статики и земли заменено патчами на каждой карте (mapdif/stadif, PLAN
    /// Стадия 2, п. 5). Сервер: Projects/Server/Network/Packets/OutgoingMapPackets.cs, SendMapPatches — int число карт,
    /// на каждую int статика, int земля. Число карт, которому не хватает данных, — ошибка пакета.
    /// </summary>
    public sealed class MapPatchesHandler
    {
        public const byte PacketId = 0xBF;
        public const ushort SubCommand = 0x18;

        private const int MapEntryLength = 8;

        private readonly WorldEnvironment _environment;

        public MapPatchesHandler(WorldEnvironment environment)
        {
            _environment = environment;
        }

        public void Handle(ref PacketReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > reader.Remaining / MapEntryLength)
            {
                throw new PacketOutOfRangeException("0xBF 0x18: число карт " + count + " не сходится с длиной пакета.");
            }

            for (var map = 0; map < count; map++)
            {
                var statics = reader.ReadInt32();
                var land = reader.ReadInt32();
                _environment.SetMapPatches(map, statics, land);
            }
        }
    }
}
