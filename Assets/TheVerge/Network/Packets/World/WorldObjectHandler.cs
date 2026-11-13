using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0xF3 World Object: предмет на земле, multi (дом, корабль) или моб. Сервер: Projects/Server/Network/Packets/
    /// OutgoingEntityPackets.cs, CreateWorldEntity для клиента High Seas — short 1, byte тип (0 предмет, 1 моб, 2 multi;
    /// 3 — «разрушаемый предмет», сервер его не шлёт), Serial, ushort графика, byte 0, short количество (дважды), short X,
    /// Y, sbyte Z, byte свет, short цвет, byte флаги, short 0. У моба направления в пакете нет — прежнее остаётся.
    /// </summary>
    public sealed class WorldObjectHandler
    {
        public const byte PacketId = 0xF3;

        private const byte TypeMobile = 1;
        private const byte TypeMulti = 2;

        private readonly EntityStore _store;

        public WorldObjectHandler(EntityStore store)
        {
            _store = store;
        }

        public void Handle(ref PacketReader reader)
        {
            reader.Skip(2); // команда, всегда 1
            var type = reader.ReadByte();
            var serial = new Serial(reader.ReadUInt32());
            var graphic = reader.ReadUInt16();
            reader.Skip(1);
            var amount = reader.ReadUInt16();
            reader.Skip(2); // то же количество ещё раз
            var position = new Point3D(reader.ReadInt16(), reader.ReadInt16(), reader.ReadSByte());
            var light = reader.ReadByte();
            var hue = reader.ReadUInt16();
            var flags = reader.ReadByte();
            if (type == TypeMobile)
            {
                var direction = _store.FindMobile(serial)?.Direction ?? Direction.North;
                _store.UpdateMobile(serial, new MobileInfo(graphic, position, direction, hue, flags, MobileInfo.NotorietyUnknown));
                return;
            }

            _store.PlaceInWorld(serial, new ItemInfo(graphic, amount, position, hue, flags, light, type == TypeMulti));
        }
    }
}
