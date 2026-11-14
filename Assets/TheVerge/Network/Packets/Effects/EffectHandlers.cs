using TheVerge.Core.Combat;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Effects
{
    /// <summary>Обработчики эффектов (0x70, 0xC0, 0xC7) — в журнал боя (PLAN Стадия 4, п. 8).</summary>
    public static class EffectHandlers
    {
        public static void Register(PacketDispatcher dispatcher, CombatLog log)
        {
            dispatcher.Register(GraphicalEffectHandler.PacketId, new GraphicalEffectHandler(log).Handle);
            dispatcher.Register(HuedEffectHandler.PacketId, new HuedEffectHandler(log).Handle);
            dispatcher.Register(ParticleEffectHandler.PacketId, new ParticleEffectHandler(log).Handle);
        }
    }
}
