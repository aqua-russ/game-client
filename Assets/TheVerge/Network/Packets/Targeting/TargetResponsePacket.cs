using TheVerge.Core.Targeting;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Targeting
{
    /// <summary>
    /// 0x6C Target Response: ответ на прицел. Сервер: Projects/UOContent/Network/Packets/IncomingTargetingPackets.cs,
    /// TargetResponse — byte тип, int номер курсора, byte флаги, uint серийник, short X, Y, byte (пропускается), sbyte Z,
    /// ushort графика; длина 19 (Register(0x6C, 19, …)).
    /// </summary>
    public static class TargetResponsePacket
    {
        public const byte PacketId = 0x6C;

        public static void Write(ref PacketWriter writer, in TargetResponse response)
        {
            writer.Write(PacketId);
            writer.Write((byte)response.Kind);
            writer.Write(response.CursorId);
            writer.Write(response.Flags);
            writer.Write(response.Serial.Value);
            writer.Write(response.Position.X);
            writer.Write(response.Position.Y);
            writer.Write((byte)0);
            writer.Write(response.Position.Z);
            writer.Write(response.Graphic);
        }
    }
}
