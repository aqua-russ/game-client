namespace TheVerge.Core.Session
{
    /// <summary>
    /// Шард в списке логин-сервера (PLAN Стадия 1, п. 6). <see cref="Index"/> — номер, который сервер ждёт в выборе шарда;
    /// <see cref="Address"/> — IPv4 строкой «a.b.c.d» (протокол UO знает только IPv4).
    /// </summary>
    public readonly struct ShardEntry
    {
        public ShardEntry(int index, string name, int fullPercent, int timeZone, string address)
        {
            Index = index;
            Name = name;
            FullPercent = fullPercent;
            TimeZone = timeZone;
            Address = address;
        }

        public int Index { get; }

        public string Name { get; }

        /// <summary>Заполненность шарда, %.</summary>
        public int FullPercent { get; }

        /// <summary>Смещение часового пояса сервера от UTC, часы.</summary>
        public int TimeZone { get; }

        public string Address { get; }

        public override string ToString() => Name + " (" + Address + ")";
    }
}
