namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// 0x80 Account Login: вход на логин-сервер. Сервер: Projects/UOContent/Network/Packets/IncomingAccountPackets.cs,
    /// AccountLogin — ReadLatin1Safe(30) логин, затем ReadLatin1Safe(30) пароль сразу после номера. Сейчас здесь только
    /// раскладка полей: по ней лог пакетов скрывает пароль; запись пакета (Write) появится в задаче 1.5.
    /// </summary>
    public static class AccountLoginPacket
    {
        public const byte PacketId = 0x80;

        /// <summary>Длина поля логина и поля пароля (строка Latin-1 фиксированной длины).</summary>
        public const int CredentialLength = 30;

        public const int UsernameOffset = 1;

        public const int PasswordOffset = UsernameOffset + CredentialLength;
    }
}
