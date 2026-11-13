namespace TheVerge.Core.Session
{
    /// <summary>Почему сервер отказал в удалении персонажа (DeleteResultType сервера; OutgoingAccountPackets.cs).</summary>
    public enum CharacterDeleteResult : byte
    {
        PasswordInvalid = 0,
        CharNotExist = 1,
        CharBeingPlayed = 2,
        CharTooYoung = 3,
        CharQueued = 4,
        BadRequest = 5,
    }
}
