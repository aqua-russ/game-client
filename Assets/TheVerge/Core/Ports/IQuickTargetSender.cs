using TheVerge.Core.Primitives;

namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Быстрые цели без курсора (ADR 0008 п. 1; PLAN Стадия 4, п. 6): бинт, заклинание, навык, по ресурсу.
    /// Реализация — Network/Packets/Targeting/QuickTargetSender.
    /// </summary>
    public interface IQuickTargetSender
    {
        void SendBandageTarget(Serial bandage, Serial target);

        /// <summary>Номер заклинания — с 1, как шлёт клиент (сервер вычитает 1).</summary>
        void SendTargetedSpell(int spellNumber, Serial target);

        void SendTargetedSkill(int skillId, Serial target);

        void SendTargetByResource(Serial item, int resourceType);
    }
}
