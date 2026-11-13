using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0x73 Ping: проверка связи. Сервер: Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs, PingReq — ReadByte
    /// номер и SendPingAck с тем же номером; длина 2 (Register(0x73, 2, …)).
    /// </summary>
    public static class PingPacket
    {
        public const byte PacketId = 0x73;

        public static void Write(ref PacketWriter writer, byte sequence)
        {
            writer.Write(PacketId);
            writer.Write(sequence);
        }
    }
}
