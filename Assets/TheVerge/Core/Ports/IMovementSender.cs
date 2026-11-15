using TheVerge.Core.Primitives;

namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Отправка шагов своего персонажа (ADR 0008 п. 1; PLAN Стадия 3, п. 5). Реализация — Network/Packets/Movement
    /// (запрос 0x02: направление с битом бега 0x80, номер шага, fastwalk-ключ 0).
    /// </summary>
    public interface IMovementSender
    {
        /// <summary>Шаг или поворот на месте: направление (без бита бега), бег и номер шага.</summary>
        void SendStep(Direction direction, bool run, byte sequence);

        /// <summary>
        /// Просьба к серверу прислать своё место заново (0x22 от клиента, 3 байта): клиент и сервер разошлись в счёте шагов.
        /// </summary>
        void SendResync();
    }
}
