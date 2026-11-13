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
        public static int LengthOf(string versionText) => 1 + 2 + versionText.Length + 1;

        /// <param name="versionText">Версия строкой, как её разбирает сервер: «7.0.95.0» (ClientVersion.ToString).</param>
        public static void Write(ref PacketWriter writer, string versionText)
        {
            writer.Write(PacketId);
            writer.Write((ushort)LengthOf(versionText));
            writer.WriteAsciiNullTerminated(versionText);
        }
    }
}
