using TheVerge.Core.Combat;
using TheVerge.Network.Packets.Effects;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// Обработчики боевых событий для журнала боя (PLAN Стадия 4, п. 8): замах, анимация, урон и эффекты. Звук (0x54)
    /// приходит через WorldEnvironment.SoundPlayed: подписать <see cref="CombatLog.OnSound"/> при сборке клиента.
    /// </summary>
    public static class CombatLogHandlers
    {
        public static void Register(PacketDispatcher dispatcher, CombatLog log)
        {
            dispatcher.Register(SwingHandler.PacketId, new SwingHandler(log).Handle);
            dispatcher.Register(DamageHandler.PacketId, new DamageHandler(log).Handle);
            dispatcher.Register(MobileAnimationHandler.PacketId, new MobileAnimationHandler(log).Handle);
            EffectHandlers.Register(dispatcher, log);
        }
    }
}
