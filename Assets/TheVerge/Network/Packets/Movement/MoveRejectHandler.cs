using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Movement
{
    /// <summary>
    /// 0x21 Move Reject: сервер отказал в шаге и сбросил счёт шагов (MovementThrottle.RejectAndReset). Сервер:
    /// Projects/Server/Network/Packets/OutgoingMovementPackets.cs, SendMovementRej — byte номер, short X, short Y,
    /// byte направление, sbyte Z: где персонаж стоит у сервера.
    /// </summary>
    public sealed class MoveRejectHandler
    {
        public const byte PacketId = 0x21;

        private readonly MovementReplies _replies;

        public MoveRejectHandler(MovementReplies replies)
        {
            _replies = replies;
        }

        public void Handle(ref PacketReader reader)
        {
            var sequence = reader.ReadByte();
            var x = reader.ReadInt16();
            var y = reader.ReadInt16();
            var direction = (Direction)reader.ReadByte();
            var z = reader.ReadSByte();
            _replies.OnReject(sequence, new Point3D(x, y, z), direction);
        }
    }
}
