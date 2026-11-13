using TheVerge.Core.Session;
using TheVerge.Network.Session;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>Обработчики пакетов входа: все разом, чтобы Bootstrap и бот регистрировали одно и то же.</summary>
    public static class AccountHandlers
    {
        public static void Register(PacketDispatcher dispatcher, LoginFlow flow)
        {
            dispatcher.Register(LoginRejectedHandler.PacketId, new LoginRejectedHandler(flow).Handle);
            dispatcher.Register(ServerListHandler.PacketId, new ServerListHandler(flow).Handle);
            dispatcher.Register(ServerRedirectHandler.PacketId, new ServerRedirectHandler(flow).Handle);
        }
    }
}
