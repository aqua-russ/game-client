namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x91 Game Login: вход на игровой сокет по ключу из 0x8C. Сервер: Projects/UOContent/Network/Packets/
    /// IncomingAccountPackets.cs, GameLogin — ReadInt32 ключ, ReadLatin1Safe(30) логин, ReadLatin1Safe(30) пароль.
    /// Сейчас здесь только раскладка полей: по ней лог пакетов скрывает пароль; запись пакета (Write) — задача 1.7.
    /// </summary>
    public static class GameLoginPacket
    {
        public const byte PacketId = 0x91;

        /// <summary>Длина поля логина и поля пароля (строка Latin-1 фиксированной длины).</summary>
        public const int CredentialLength = 30;

        private const int AuthIdOffset = 1;
        private const int AuthIdLength = 4;

        public const int UsernameOffset = AuthIdOffset + AuthIdLength;

        public const int PasswordOffset = UsernameOffset + CredentialLength;
    }
}
