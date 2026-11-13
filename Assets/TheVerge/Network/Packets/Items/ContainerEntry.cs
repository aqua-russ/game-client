using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>
    /// Предмет в контейнере, 20 байт — общая часть 0x3C и 0x25. Сервер: Projects/Server/Network/Packets/
    /// OutgoingContainerPackets.cs (SendContainerContent, SendContainerContentUpdate; клиент 6.0.1.7+ — с клеткой сетки):
    /// Serial, ushort графика, byte 0, ushort количество, short X, Y, byte клетка сетки, Serial контейнера, ushort цвет.
    /// </summary>
    internal readonly struct ContainerEntry
    {
        public const int Length = 20;

        private ContainerEntry(Serial serial, ItemInfo info, Serial container, byte gridIndex)
        {
            Serial = serial;
            Info = info;
            Container = container;
            GridIndex = gridIndex;
        }

        public Serial Serial { get; }

        public ItemInfo Info { get; }

        public Serial Container { get; }

        public byte GridIndex { get; }

        public static ContainerEntry Read(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var graphic = reader.ReadUInt16();
            reader.Skip(1);
            var amount = reader.ReadUInt16();
            var x = reader.ReadInt16();
            var y = reader.ReadInt16();
            var grid = reader.ReadByte();
            var container = new Serial(reader.ReadUInt32());
            var hue = reader.ReadUInt16();
            var info = new ItemInfo(graphic, amount, new Point3D(x, y, 0), hue, 0, 0);
            return new ContainerEntry(serial, info, container, grid);
        }
    }
}
