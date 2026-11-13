using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x91 Game Login: вход на игровой сокет по ключу из 0x8C. Сервер: Projects/UOContent/Network/Packets/
    /// IncomingAccountPackets.cs, GameLogin — ReadInt32 ключ, ReadLatin1Safe(30) логин, ReadLatin1Safe(30) пароль.
    /// По этой же раскладке лог пакетов скрывает пароль.
    /// </summary>
    public static class GameLoginPacket
    {
        public const byte PacketId = 0x91;

        /// <summary>Длина поля логина и поля пароля (строка Latin-1 фиксированной длины).</summary>
        public const int CredentialLength = 30;

        private const int AuthIdOffset = 1;
        /// <summary>Ключ: int в 0x91 и столько же сырых байт seed перед ним.</summary>
        public const int AuthIdLength = 4;

        public const int UsernameOffset = AuthIdOffset + AuthIdLength;

        public const int PasswordOffset = UsernameOffset + CredentialLength;

        public static void Write(ref PacketWriter writer, int authKey, LoginCredentials credentials)
        {
            writer.Write(PacketId);
            writer.Write(authKey);
            writer.WriteLatin1(credentials.Username, CredentialLength);
            writer.WriteLatin1(credentials.Password, CredentialLength);
        }
    }
}
