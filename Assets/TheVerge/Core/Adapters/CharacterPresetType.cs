namespace TheVerge.Core
{
    /// <summary>
    /// Стартовые пресеты создания персонажа по GDD (docs/gdd/01-character-creation.md §1.2).
    /// Каждый пресет распределяет 80 статов и 3 навыка по формуле 50.0 / 50.0 / 10.0.
    /// </summary>
    public enum CharacterPresetType : byte
    {
        MinerBlacksmith = 0,
        Warrior = 1,
        Mage = 2,
        LumberjackCarpenter = 3,
        Archer = 4,
        Alchemist = 5
    }
}
