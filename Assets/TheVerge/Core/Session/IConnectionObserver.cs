namespace TheVerge.Core.Session
{
    /// <summary>
    /// Что ядро узнаёт о соединении: оно установлено или закончилось и почему. Сообщает NetworkSession в главном потоке,
    /// по порядку с пакетами.
    /// </summary>
    public interface IConnectionObserver
    {
        void OnConnected();

        void OnDisconnected(DisconnectReason reason, string detail);
    }
}
