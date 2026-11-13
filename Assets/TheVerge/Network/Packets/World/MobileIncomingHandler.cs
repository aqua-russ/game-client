using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.World
{
    /// <summary>
    /// 0x78 Mobile Incoming: моб и всё, что на нём видно. Сервер: Projects/Server/Network/Packets/OutgoingMobilePackets.cs,
    /// SendMobileIncoming — Serial, short тело, short X, Y, sbyte Z, byte направление, short цвет, byte флаги, byte
    /// «злость»; затем на каждый предмет Serial, ushort графика, byte слой, short цвет (новый формат 7.0.33.1+: цвет есть
    /// всегда, флага 0x8000 нет); в конце int 0. Список полный, поэтому не повторённое надетое снимается.
    /// Длина обязана сходиться со списком: иначе пакет пропускается целиком, мир не меняется.
    /// </summary>
    public sealed class MobileIncomingHandler
    {
        public const byte PacketId = 0x78;

        private const int MobileFieldsLength = 16;
        private const int ItemLength = 9;
        private const int EndMarkLength = 4;

        private readonly EntityStore _store;

        public MobileIncomingHandler(EntityStore store)
        {
            _store = store;
        }

        public void Handle(ref PacketReader reader)
        {
            var itemCount = CountItems(reader);
            var serial = new Serial(reader.ReadUInt32());
            var body = reader.ReadUInt16();
            var x = reader.ReadInt16();
            var y = reader.ReadInt16();
            var z = reader.ReadSByte();
            var direction = (Direction)reader.ReadByte();
            var hue = reader.ReadUInt16();
            var flags = reader.ReadByte();
            var notoriety = reader.ReadByte();
            if (_store.UpdateMobile(serial, new MobileInfo(body, new Point3D(x, y, z), direction, hue, flags, notoriety)) != WorldResult.Applied)
            {
                return;
            }

            _store.BeginEquipment(serial);
            for (var i = 0; i < itemCount; i++)
            {
                var item = new Serial(reader.ReadUInt32());
                var graphic = reader.ReadUInt16();
                var layer = (Layer)reader.ReadByte();
                var itemHue = reader.ReadUInt16();
                _store.Equip(item, new ItemInfo(graphic, 1, Point3D.Zero, itemHue, 0, 0), serial, layer);
            }

            _store.EndEquipment(serial);
        }

        /// <summary>Сколько предметов в списке; длина, не совпадающая со списком и концом int 0, — ошибка пакета.</summary>
        private static int CountItems(PacketReader probe)
        {
            var listLength = probe.Remaining - MobileFieldsLength - EndMarkLength;
            if (listLength < 0 || listLength % ItemLength != 0)
            {
                throw new PacketOutOfRangeException("0x78: длина " + probe.Remaining + " не сходится со списком предметов по " + ItemLength + " байт.");
            }

            probe.Skip(MobileFieldsLength);
            var count = listLength / ItemLength;
            for (var i = 0; i < count; i++)
            {
                if (probe.ReadUInt32() == 0)
                {
                    throw new PacketOutOfRangeException("0x78: конец списка (0) раньше конца пакета.");
                }

                probe.Skip(ItemLength - 4);
            }

            if (probe.ReadUInt32() != 0)
            {
                throw new PacketOutOfRangeException("0x78: нет конца списка предметов (int 0).");
            }

            return count;
        }
    }
}
