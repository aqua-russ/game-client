namespace TheVerge.Core.World
{
    /// <summary>Числа полного статуса своего персонажа (0x11 версии 3, OutgoingMobilePackets.CreateMobileStatus).</summary>
    public readonly struct StatusAttributes
    {
        public StatusAttributes(int str, int dex, int intel, int gold, int armor, int weight, int statCap)
        {
            Str = str;
            Dex = dex;
            Int = intel;
            Gold = gold;
            Armor = armor;
            Weight = weight;
            StatCap = statCap;
        }

        public int Str { get; }
        public int Dex { get; }
        public int Int { get; }

        /// <summary>Всё золото при себе (Mobile.TotalGold).</summary>
        public int Gold { get; }

        /// <summary>Броня: в UOR — ArmorRating, округлённый (AOS — физическое сопротивление).</summary>
        public int Armor { get; }

        /// <summary>Вес: тело + всё при себе (Mobile.BodyWeight + TotalWeight).</summary>
        public int Weight { get; }

        /// <summary>Предел суммы статов.</summary>
        public int StatCap { get; }
    }
}
