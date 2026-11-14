using TheVerge.Core.Status;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Status
{
    /// <summary>Обработчики пакетов состояния мобов (PLAN Стадия 4, п. 4) — все разом для Bootstrap и бота.</summary>
    public static class StatusHandlers
    {
        public static void Register(PacketDispatcher dispatcher, MobileStatusUpdates updates)
        {
            dispatcher.Register(MobileStatusHandler.PacketId, new MobileStatusHandler(updates).Handle);
            dispatcher.Register(MobileVitalHandler.HitsPacketId, new MobileVitalHandler(updates, VitalKind.Hits).Handle);
            dispatcher.Register(MobileVitalHandler.ManaPacketId, new MobileVitalHandler(updates, VitalKind.Mana).Handle);
            dispatcher.Register(MobileVitalHandler.StamPacketId, new MobileVitalHandler(updates, VitalKind.Stam).Handle);
            dispatcher.Register(MobileAttributesHandler.PacketId, new MobileAttributesHandler(updates).Handle);
            dispatcher.Register(HealthbarHandler.PacketId, new HealthbarHandler(updates).Handle);
            dispatcher.Register(ExtendedStatsHandler.PacketId, ExtendedStatsHandler.SubCommand, new ExtendedStatsHandler(updates).Handle);
        }
    }
}
