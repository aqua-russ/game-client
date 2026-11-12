using TheVerge.Core.Session;
using TheVerge.Network.IO;

namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x80 Account Login: вход на логин-сервер. Сервер: Projects/UOContent/Network/Packets/IncomingAccountPackets.cs,
    /// AccountLogin — ReadLatin1Safe(30) логин, затем ReadLatin1Safe(30) пароль сразу после номера; длина 62
    /// (Register(0x80, …, 62)), последний байт сервер не читает. По этой же раскладке лог пакетов скрывает пароль.
    /// </summary>
    public static class AccountLoginPacket
    {
        public const byte PacketId = 0x80;

        /// <summary>Длина поля логина и поля пароля (строка Latin-1 фиксированной длины).</summary>
        public const int CredentialLength = 30;

        public const int UsernameOffset = 1;

        public const int PasswordOffset = UsernameOffset + CredentialLength;

        /// <summary>Байт после пароля: сервер его не читает, пишем 0.</summary>
        private const int TrailingBytes = 1;

        public static void Write(ref PacketWriter writer, LoginCredentials credentials)
        {
            writer.Write(PacketId);
            writer.WriteLatin1(credentials.Username, CredentialLength);
            writer.WriteLatin1(credentials.Password, CredentialLength);
            writer.WriteZeros(TrailingBytes);
        }
    }
}
