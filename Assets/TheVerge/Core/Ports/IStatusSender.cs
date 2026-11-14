using TheVerge.Core.Primitives;

namespace TheVerge.Core.Ports
{
    /// <summary>Отправка запросов о состоянии мобов (ADR 0008 п. 1). Реализация — Network/Packets/Status/StatusSender.</summary>
    public interface IStatusSender
    {
        /// <summary>Попросить у сервера статус моба (ответ — 0x11).</summary>
        void SendStatusQuery(Serial serial);
    }
}
