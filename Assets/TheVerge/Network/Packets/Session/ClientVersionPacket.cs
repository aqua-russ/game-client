using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0xBD Client Version: ответ на запрос версии. Сервер: Projects/UOContent/Network/Packets/IncomingAccountPackets.cs,
    /// ClientVersion — ReadAscii() до нуля, «7.0.95.0»; по ответу сервер меряет RTT. Длина — в заголовке.
    /// </summary>
    public static class ClientVersionPacket
    {
        public const byte PacketId = 0xBD;

        /// <summary>Номер, длина (2), строка версии и ноль.</summary>
        public static int LengthOf(ClientVersion version) => 1 + 2 + version.ToString().Length + 1;

        public static void Write(ref PacketWriter writer, ClientVersion version)
        {
            writer.Write(PacketId);
            writer.Write((ushort)LengthOf(version));
            writer.WriteAsciiNullTerminated(version.ToString());
        }
    }
}
