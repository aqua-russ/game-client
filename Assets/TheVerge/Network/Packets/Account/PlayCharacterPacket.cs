using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x5D Play Character: войти в мир персонажем. Сервер: Projects/UOContent/Network/Packets/IncomingAccountPackets.cs,
    /// PlayCharacter — пропускает 36 байт (0xEDEDEDED, имя 30 байт, 2 байта), ReadInt32 флаги карт клиента, пропускает 24,
    /// ReadInt32 слот, пропускает 4 (IP клиента); длина 73 (Register(0x5D, …, 73)).
    /// </summary>
    public static class PlayCharacterPacket
    {
        public const byte PacketId = 0x5D;

        /// <summary>Метка, с которой клиенты начинают пакет; сервер её пропускает.</summary>
        private const uint Marker = 0xEDEDEDED;

        private const int NameLength = 30;
        private const int UnreadAfterName = 2;
        private const int UnreadAfterFlags = 24;
        private const int UnreadClientIp = 4;

        /// <param name="name">Имя персонажа; сервер его не читает, пишем как в списке — UTF-8 (расширение E1).</param>
        public static void Write(ref PacketWriter writer, int slot, string name)
        {
            writer.Write(PacketId);
            writer.Write(Marker);
            writer.WriteUtf8(name, NameLength);
            writer.WriteZeros(UnreadAfterName);
            writer.Write(ClientMapFlags.FeluccaOnly);
            writer.WriteZeros(UnreadAfterFlags);
            writer.Write(slot);
            writer.WriteZeros(UnreadClientIp);
        }
    }
}
