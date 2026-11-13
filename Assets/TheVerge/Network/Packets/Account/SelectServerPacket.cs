using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0xA0 Select Server: выбор шарда. Сервер: Projects/UOContent/Network/Packets/IncomingAccountPackets.cs, PlayServer —
    /// ReadInt16 номер шарда в списке; длина 3 (Register(0xA0, …, 3)).
    /// </summary>
    public static class SelectServerPacket
    {
        public const byte PacketId = 0xA0;

        public static void Write(ref PacketWriter writer, int index)
        {
            writer.Write(PacketId);
            writer.Write((short)index);
        }
    }
}
