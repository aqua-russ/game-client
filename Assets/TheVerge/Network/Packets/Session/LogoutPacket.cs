using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0xD1 Logout: запрос выхода. Сервер: Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs, LogoutReq —
    /// содержимое не читает и отвечает SendLogoutAck; длина 2 (Register(0xD1, 2, …)).
    /// </summary>
    public static class LogoutPacket
    {
        public const byte PacketId = 0xD1;

        public static void Write(ref PacketWriter writer)
        {
            writer.Write(PacketId);
            writer.WriteZeros(1); // сервер не читает
        }
    }
}
