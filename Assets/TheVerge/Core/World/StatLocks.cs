namespace TheVerge.Core.World
{
    /// <summary>Замки силы, ловкости и интеллекта своего персонажа (0xBF 0x19, команда 2 — OutgoingPlayerPackets.SendStatLockInfo).</summary>
    public readonly struct StatLocks
    {
        public StatLocks(StatLock str, StatLock dex, StatLock intel)
        {
            Str = str;
            Dex = dex;
            Int = intel;
        }

        public StatLock Str { get; }
        public StatLock Dex { get; }
        public StatLock Int { get; }
    }
}
