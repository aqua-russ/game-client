namespace TheVerge.Core.Session
{
    /// <summary>
    /// Названия навыков по номеру сервера (SkillName; Projects/Server/Skills.cs). До файлов данных (skills.mul, Стадия 2Б)
    /// показываются эти названия; номер — тот, что уходит серверу.
    /// </summary>
    public static class SkillNames
    {
        private static readonly string[] Names =
        {
            "Alchemy", "Anatomy", "Animal Lore", "Item ID", "Arms Lore", "Parry", "Begging", "Blacksmith", "Fletching",
            "Peacemaking", "Camping", "Carpentry", "Cartography", "Cooking", "Detect Hidden", "Discordance",
            "Evaluating Intelligence", "Healing", "Fishing", "Forensics", "Herding", "Hiding", "Provocation", "Inscribe",
            "Lockpicking", "Magery", "Magic Resist", "Tactics", "Snooping", "Musicianship", "Poisoning", "Archery",
            "Spirit Speak", "Stealing", "Tailoring", "Animal Taming", "Taste ID", "Tinkering", "Tracking", "Veterinary",
            "Swords", "Macing", "Fencing", "Wrestling", "Lumberjacking", "Mining", "Meditation", "Stealth", "Remove Trap",
            "Necromancy", "Focus", "Chivalry", "Bushido", "Ninjitsu", "Spellweaving", "Mysticism", "Imbuing", "Throwing",
        };

        /// <summary>Сколько навыков знает сервер (номера 0 … Count − 1).</summary>
        public static int Count => Names.Length;

        /// <summary>Название навыка; номера вне списка сервера — «навык N».</summary>
        public static string Name(int skill) => skill >= 0 && skill < Names.Length ? Names[skill] : "навык " + skill;
    }
}
