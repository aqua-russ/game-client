using TheVerge.Core.Primitives;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Movement
{
    /// <summary>
    /// 0x02 Move Request: шаг или поворот своего персонажа. Сервер: Projects/UOContent/Network/Packets/
    /// IncomingMovementPackets.cs, MovementReq — byte направление (бит Direction.Running — бег), byte номер шага, uint ключ
    /// fastwalk (на серверах без fastwalk не используется — шлём 0). Сверено с эталоном ClassicUO golden/02-walk-20.tsv.
    /// </summary>
    public static class MoveRequestPacket
    {
        public const byte PacketId = 0x02;

        private const uint NoFastWalkKey = 0;

        public static void Write(ref PacketWriter writer, Direction direction, bool run, byte sequence)
        {
            var facing = direction & Direction.FacingMask;
            writer.Write(PacketId);
            writer.Write((byte)(run ? facing | Direction.Running : facing));
            writer.Write(sequence);
            writer.Write(NoFastWalkKey);
        }
    }
}
