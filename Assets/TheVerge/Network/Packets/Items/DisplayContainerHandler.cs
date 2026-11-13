using TheVerge.Core.Items;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>
    /// 0x24 Display Container: открыть окно контейнера. Сервер: Projects/Server/Network/Packets/OutgoingContainerPackets.cs,
    /// SendDisplayContainer — Serial, ushort окно (gump), для клиента High Seas ещё short 0x7D.
    /// </summary>
    public sealed class DisplayContainerHandler
    {
        public const byte PacketId = 0x24;

        private readonly ContainerContents _contents;

        public DisplayContainerHandler(ContainerContents contents)
        {
            _contents = contents;
        }

        public void Handle(ref PacketReader reader)
        {
            var serial = new Serial(reader.ReadUInt32());
            var gump = reader.ReadUInt16();
            _contents.OnOpen(serial, gump);
        }
    }
}
