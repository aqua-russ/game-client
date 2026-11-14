using TheVerge.Core.Primitives;

namespace TheVerge.Core.Ports
{
    /// <summary>Отправка боевых запросов (ADR 0008 п. 1): стойка и атака. Реализация — Network/Packets/Combat/CombatSender.</summary>
    public interface ICombatSender
    {
        void SendWarMode(bool warMode);

        void SendAttack(Serial target);
    }
}
