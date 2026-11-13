using TheVerge.Core.Primitives;

namespace TheVerge.Core.Ports
{
    /// <summary>Отправка действий с предметами (ADR 0008 п. 1). Реализация — Network/Packets/Items/ItemSender.</summary>
    public interface IItemSender
    {
        /// <summary>Двойной клик по предмету или мобу (открыть контейнер, использовать).</summary>
        void SendUse(Serial serial);
    }
}
