using TheVerge.Core.Session;

namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Отправка служебных пакетов игровой сессии (ADR 0008 п. 1): ответ на запрос версии, язык, дальность обзора, ping,
    /// выход. Реализация — Network/Packets/Session/SessionSender.
    /// </summary>
    public interface ISessionSender
    {
        void SendClientVersion(ClientVersion version);

        /// <summary>Язык клиента — 3 буквы (ENU, RUS …).</summary>
        void SendLanguage(string language);

        void SendViewRange(int range);

        void SendPing(byte sequence);

        void SendLogout();
    }
}
