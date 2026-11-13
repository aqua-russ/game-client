using TheVerge.Core.Items;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>Обработчики пакетов контейнеров (PLAN Стадия 2, п. 9а) — все разом для Bootstrap и бота.</summary>
    public static class ItemHandlers
    {
        public static void Register(PacketDispatcher dispatcher, ContainerContents contents)
        {
            dispatcher.Register(DisplayContainerHandler.PacketId, new DisplayContainerHandler(contents).Handle);
            dispatcher.Register(ContainerContentHandler.PacketId, new ContainerContentHandler(contents).Handle);
            dispatcher.Register(ContainerItemHandler.PacketId, new ContainerItemHandler(contents).Handle);
        }
    }
}
