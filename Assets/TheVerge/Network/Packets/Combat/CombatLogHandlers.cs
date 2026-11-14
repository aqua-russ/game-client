using TheVerge.Core.Combat;
using TheVerge.Network.Packets.Effects;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Combat
{
    /// <summary>
    /// Обработчики боевых событий для журнала боя (PLAN Стадия 4, п. 8): замах, анимация, урон, эффекты и смерть (0xAF, п. 9). Звук (0x54)
    /// приходит через WorldEnvironment.SoundPlayed: подписать <see cref="CombatLog.OnSound"/> при сборке клиента.
    /// </summary>
    public static class CombatLogHandlers
    {
        public static void Register(PacketDispatcher dispatcher, CombatLog log)
        {
            dispatcher.Register(SwingHandler.PacketId, new SwingHandler(log).Handle);
            dispatcher.Register(DamageHandler.PacketId, new DamageHandler(log).Handle);
            dispatcher.Register(MobileAnimationHandler.PacketId, new MobileAnimationHandler(log).Handle);
            dispatcher.Register(DeathAnimationHandler.PacketId, new DeathAnimationHandler(log).Handle);
            EffectHandlers.Register(dispatcher, log);
        }
    }
}
