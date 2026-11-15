using TheVerge.Core.Movement;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Movement
{
    /// <summary>
    /// Обработчики ответов сервера на шаги (PLAN Стадия 3, п. 6) — для Bootstrap и бота. 0x20 о своём персонаже и смену
    /// карты 0xBF 0x08 разбирают обработчики мира (<see cref="World.WorldHandlers"/> с теми же <see cref="MovementReplies"/>).
    /// </summary>
    public static class MovementHandlers
    {
        public static void Register(PacketDispatcher dispatcher, MovementReplies replies)
        {
            dispatcher.Register(MoveAckHandler.PacketId, new MoveAckHandler(replies).Handle);
            dispatcher.Register(MoveRejectHandler.PacketId, new MoveRejectHandler(replies).Handle);
        }
    }
}
