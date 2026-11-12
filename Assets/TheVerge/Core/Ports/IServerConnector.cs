namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Подключение к серверу (ADR 0013 п. 7). Вход идёт через два соединения: логин-сервер, затем игровой сервер по адресу
    /// из редиректа; новое подключение закрывает прежнее. Итог приходит в IConnectionObserver.
    /// </summary>
    public interface IServerConnector
    {
        void Connect(string host, int port);
    }
}
