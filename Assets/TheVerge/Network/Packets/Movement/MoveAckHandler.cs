using TheVerge.Core.Movement;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Movement
{
    /// <summary>
    /// 0x22 Move Ack: сервер выполнил шаг или поворот. Сервер: Projects/Server/Network/Packets/OutgoingMovementPackets.cs,
    /// SendMovementAck — byte номер (счётчик сервера: Mobile.Move шлёт Sequence до его увеличения), byte известность своего
    /// персонажа (Notoriety.Compute(m, m)).
    /// </summary>
    public sealed class MoveAckHandler
    {
        public const byte PacketId = 0x22;

        private readonly MovementReplies _replies;

        public MoveAckHandler(MovementReplies replies)
        {
            _replies = replies;
        }

        public void Handle(ref PacketReader reader)
        {
            var sequence = reader.ReadByte();
            _replies.OnAck(sequence, reader.ReadByte());
        }
    }
}
