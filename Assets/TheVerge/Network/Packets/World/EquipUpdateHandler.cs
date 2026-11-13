using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x2E Equip Update: предмет надет на моба (и волосы, борода). Сервер: Projects/Server/Network/Packets/
    /// OutgoingEquipmentPackets.cs, SendEquipUpdate — Serial, short графика, ushort слой (байт 0 и слой), Serial моба,
    /// short цвет; волосы — OutgoingVirtualHairPackets.cs, CreateHairEquipUpdatePacket, те же байты.
    /// </summary>
    public sealed class EquipUpdateHandler
    {
        public const byte PacketId = 0x2E;

        private readonly EntityStore _store;

        public EquipUpdateHandler(EntityStore store)
        {
            _store = store;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var graphic = reader.ReadUInt16();
            reader.Skip(1);
            var layer = (Layer)reader.ReadByte();
            var mobile = new Serial(reader.ReadUInt32());
            var hue = reader.ReadUInt16();
            _store.Equip(serial, new ItemInfo(graphic, 1, Point3D.Zero, hue, 0, 0), mobile, layer);
        }
    }
}
