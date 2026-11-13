using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x83 Delete Character. Сервер: Projects/UOContent/Network/Packets/IncomingAccountPackets.cs, DeleteCharacter —
    /// пропускает 30 байт (поле пароля, сервер его не проверяет), ReadInt32 слот; затем 4 байта IP клиента; длина 39.
    /// </summary>
    public static class DeleteCharacterPacket
    {
        public const byte PacketId = 0x83;

        private const int PasswordLength = 30;
        private const int ClientIpLength = 4;

        public static void Write(ref PacketWriter writer, int slot)
        {
            writer.Write(PacketId);
            writer.WriteZeros(PasswordLength);
            writer.Write(slot);
            writer.WriteZeros(ClientIpLength);
        }
    }
}
