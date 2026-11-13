using TheVerge.Core.Targeting;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Targeting
{
    /// <summary>Обработчики прицела (PLAN Стадия 2, п. 10а) — для Bootstrap и бота.</summary>
    public static class TargetHandlers
    {
        public static void Register(PacketDispatcher dispatcher, TargetCursor cursor) =>
            dispatcher.Register(TargetRequestHandler.PacketId, new TargetRequestHandler(cursor).Handle);
    }
}
