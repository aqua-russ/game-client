using TheVerge.Core.World;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.World
{
    /// <summary>Обработчики пакетов мира (PLAN Стадия 2, п. 9) — все разом для Bootstrap и бота.</summary>
    public static class WorldHandlers
    {
        public static void Register(PacketDispatcher dispatcher, EntityStore store)
        {
            dispatcher.Register(MobileIncomingHandler.PacketId, new MobileIncomingHandler(store).Handle);
            dispatcher.Register(MobileUpdateHandler.PacketId, new MobileUpdateHandler(store).Handle);
            dispatcher.Register(MobileMovingHandler.PacketId, new MobileMovingHandler(store).Handle);
            dispatcher.Register(EquipUpdateHandler.PacketId, new EquipUpdateHandler(store).Handle);
            dispatcher.Register(RemoveEntityHandler.PacketId, new RemoveEntityHandler(store).Handle);
        }
    }
}
