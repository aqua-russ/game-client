using TheVerge.Core.Targeting;

namespace TheVerge.Core.Ports
{
    /// <summary>Отправка ответа на прицел (ADR 0008 п. 1). Реализация — Network/Packets/Targeting/TargetSender.</summary>
    public interface ITargetSender
    {
        void SendTarget(in TargetResponse response);
    }
}
