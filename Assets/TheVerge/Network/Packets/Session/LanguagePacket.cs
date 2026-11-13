using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Session
{
    /// <summary>
    /// 0xBF 0x0B Language: язык клиента. Сервер: Projects/UOContent/Network/Packets/IncomingExtendedCommandPackets.cs,
    /// Language — ReadAscii(4) в Mobile.Language. Пакет: 0xBF, ushort длина, ushort подкоманда, 4 байта ASCII.
    /// </summary>
    public static class LanguagePacket
    {
        public const byte PacketId = 0xBF;

        public const ushort SubCommand = 0x0B;

        private const int LanguageLength = 4;

        /// <summary>Номер, длина, подкоманда, 4 байта языка.</summary>
        public const int Length = 1 + 2 + 2 + LanguageLength;

        public static void Write(ref PacketWriter writer, string language)
        {
            writer.Write(PacketId);
            writer.Write((ushort)Length);
            writer.Write(SubCommand);
            writer.WriteAscii(language, LanguageLength);
        }
    }
}
