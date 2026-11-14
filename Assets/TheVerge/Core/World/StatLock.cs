namespace TheVerge.Core.World
{
    /// <summary>Замок стата, как StatLockType сервера: растёт, падает или стоит.</summary>
    public enum StatLock : byte
    {
        Up = 0,
        Down = 1,
        Locked = 2,
    }
}
