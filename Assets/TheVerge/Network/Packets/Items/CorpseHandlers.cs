using TheVerge.Core.World;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Items
{
    /// <summary>Обработчики трупов (PLAN Стадия 4, п. 9): вещи по слоям (0x89). Сам труп — обычный предмет 0x2006.</summary>
    public static class CorpseHandlers
    {
        public static void Register(PacketDispatcher dispatcher, CorpseEquipment corpses)
        {
            dispatcher.Register(CorpseEquipmentHandler.PacketId, new CorpseEquipmentHandler(corpses).Handle);
        }
    }
}
