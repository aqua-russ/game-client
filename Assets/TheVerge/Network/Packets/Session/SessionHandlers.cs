using TheVerge.Core.Session;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>Обработчики пакетов игровой сессии: вход в мир и запросы сервера — все разом для Bootstrap и бота.</summary>
    public static class SessionHandlers
    {
        public static void Register(PacketDispatcher dispatcher, WorldEntry world, ServerRequests requests)
        {
            dispatcher.Register(LoginConfirmHandler.PacketId, new LoginConfirmHandler(world).Handle);
            dispatcher.Register(LoginCompleteHandler.PacketId, new LoginCompleteHandler(world).Handle);
            dispatcher.Register(ClientVersionRequestHandler.PacketId, new ClientVersionRequestHandler(requests).Handle);
        }
    }
}
