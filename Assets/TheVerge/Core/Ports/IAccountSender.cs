using TheVerge.Core.Session;

namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Отправка пакетов входа (ADR 0008 п. 1): логин-сервер, выбор шарда, игровой сервер, персонажи. Реализация —
    /// Network/Packets/Account/AccountSender. Пакет уходит в сокет в конце тика.
    /// </summary>
    public interface IAccountSender
    {
        /// <summary>Seed и версия клиента — первое, что получает логин-сервер.</summary>
        void SendLoginSeed(int seed, ClientVersion version);

        void SendAccountLogin(LoginCredentials credentials);

        /// <summary>Выбор шарда: <paramref name="index"/> — номер шарда из списка сервера (<see cref="ShardEntry.Index"/>).</summary>
        void SendSelectShard(int index);

        /// <summary>
        /// Вход на игровой сервер — первое, что он получает: ключ из редиректа и логин. Сжатие ответов сервера включается
        /// здесь же, до отправки: ответ на вход уже сжат.
        /// </summary>
        void SendGameLogin(int authKey, LoginCredentials credentials);
    }
}
