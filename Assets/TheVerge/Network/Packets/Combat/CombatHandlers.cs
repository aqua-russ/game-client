using TheVerge.Core.Combat;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>Обработчики боевых пакетов (PLAN Стадия 4, п. 7–8) — все разом для Bootstrap и бота.</summary>
    public static class CombatHandlers
    {
        public static void Register(PacketDispatcher dispatcher, CombatState state)
        {
            dispatcher.Register(WarModeHandler.PacketId, new WarModeHandler(state).Handle);
            dispatcher.Register(ChangeCombatantHandler.PacketId, new ChangeCombatantHandler(state).Handle);
        }
    }
}
