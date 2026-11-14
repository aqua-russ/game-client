using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.Status
{
    /// <summary>Запросы о состоянии мобов (PLAN Стадия 4, п. 4): статус чужого моба — ответ придёт пакетом 0x11.</summary>
    public sealed class StatusCommands
    {
        private readonly IStatusSender _sender;

        public StatusCommands(IStatusSender sender)
        {
            _sender = sender;
        }

        /// <summary>Попросить статус моба; серийник 0 — ничего не уходит (сервер такого моба не найдёт).</summary>
        public void RequestStatus(Serial serial)
        {
            if (serial != Serial.Zero)
            {
                _sender.SendStatusQuery(serial);
            }
        }
    }
}
