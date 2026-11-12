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
    }
}
