namespace TheVerge.Core.Session
{
    /// <summary>
    /// Почему игровой сервер не пустил персонажа (PMMessage сервера; Projects/Server/Network/Packets/
    /// OutgoingAccountPackets.cs). Сейчас сервер шлёт только <see cref="CharInWorld"/> — другой персонаж аккаунта в мире.
    /// </summary>
    public enum CharacterRejectReason : byte
    {
        None = 0,
        CharNoExist = 1,
        CharExists = 2,
        CharInWorld = 5,
        LoginSyncError = 6,
        IdleWarning = 7,
    }
}
