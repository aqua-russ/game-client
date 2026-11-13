using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0xC8 View Range: дальность обзора клиента. Сервер: Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs,
    /// SetUpdateRange — значение не читает и отвечает 0xC8 с 18; длина 2.
    /// </summary>
    public static class ViewRangePacket
    {
        public const byte PacketId = 0xC8;

        public static void Write(ref PacketWriter writer, int range)
        {
            writer.Write(PacketId);
            writer.Write((byte)range);
        }
    }
}
