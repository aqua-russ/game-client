using TheVerge.Core.Ports;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Запросы сервера, на которые клиент отвечает в любом состоянии сессии (PLAN Стадия 1, п. 10): запрос версии
    /// приходит после выбора персонажа и во время ходьбы — по ответу сервер меряет задержку (RTT).
    /// </summary>
    public sealed class ServerRequests
    {
        private readonly ISessionSender _sender;

        public ServerRequests(ISessionSender sender)
        {
            _sender = sender;
        }

        public void OnClientVersionRequest() => _sender.SendClientVersion(ServerLimits.ClientVersion);
    }
}
