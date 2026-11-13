using TheVerge.Core.Items;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>
    /// 0x25 Container Item: один предмет появился или изменился в контейнере. Сервер: Projects/Server/Network/Packets/
    /// OutgoingContainerPackets.cs, SendContainerContentUpdate — те же 20 байт, что предмет в 0x3C (<see cref="ContainerEntry"/>).
    /// </summary>
    public sealed class ContainerItemHandler
    {
        public const byte PacketId = 0x25;

        private readonly ContainerContents _contents;

        public ContainerItemHandler(ContainerContents contents)
        {
            _contents = contents;
        }

        public void Handle(ref PacketReader reader)
        {
            var entry = ContainerEntry.Read(ref reader);
            _contents.AddContent(entry.Serial, entry.Info, entry.Container, entry.GridIndex);
        }
    }
}
