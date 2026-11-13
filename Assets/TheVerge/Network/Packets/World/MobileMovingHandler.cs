using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x77 Mobile Moving: чужой моб сдвинулся или изменился. Сервер: Projects/Server/Network/Packets/OutgoingMobilePackets.cs,
    /// CreateMobileMoving — Serial, short тело, short X, Y, sbyte Z, byte направление, short цвет, byte флаги, byte «злость».
    /// Неизвестный моб — заглушка (PLAN Стадия 2, п. 7).
    /// </summary>
    public sealed class MobileMovingHandler
    {
        public const byte PacketId = 0x77;

        private readonly EntityStore _store;

        public MobileMovingHandler(EntityStore store)
        {
            _store = store;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var body = reader.ReadUInt16();
            var x = reader.ReadInt16();
            var y = reader.ReadInt16();
            var z = reader.ReadSByte();
            var direction = (Direction)reader.ReadByte();
            var hue = reader.ReadUInt16();
            var flags = reader.ReadByte();
            var notoriety = reader.ReadByte();
            _store.MoveMobile(serial, new MobileInfo(body, new Point3D(x, y, z), direction, hue, flags, notoriety));
        }
    }
}
