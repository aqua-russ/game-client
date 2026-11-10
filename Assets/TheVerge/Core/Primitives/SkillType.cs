namespace TheVerge.Core
{
    /// <summary>
    /// Реестр активных навыков персонажа (устаревшее: список навыков GDD, будет заменён навыками ModernUO).
    /// Идентификаторы строго соответствуют каноническому enum SkillName сервера ModernUO.
    /// </summary>
    public enum SkillType : byte
    {
        Alchemy = 0,
        Anatomy = 1,
        AnimalLore = 2,
        Parry = 5,
        Blacksmith = 7,
        Fletching = 8,
        Carpentry = 11,
        Cooking = 13,
        Healing = 17,
        Fishing = 18,
        Hiding = 21,
        Inscribe = 23,
        Lockpicking = 24,
        Magery = 25,
        MagicResist = 26,
        Tactics = 27,
        Archery = 31,
        Stealing = 33,
        Tailoring = 34,
        AnimalTaming = 35,
        Tinkering = 37,
        Swords = 40,
        Macing = 41,
        Fencing = 42,
        Lumberjacking = 44,
        Mining = 45,
        Meditation = 46
    }
}
