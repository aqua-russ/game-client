namespace TheVerge.Core.Session
{
    /// <summary>
    /// Что игрок выбрал в окне создания персонажа (PLAN Стадия 1, п. 8; правила — docs/TARGET.md §4,
    /// <see cref="CharacterCreationRules"/>). Навыки — номера навыков сервера (SkillName) и значения.
    /// </summary>
    public sealed class NewCharacter
    {
        public string Name { get; set; } = string.Empty;

        public bool Female { get; set; }

        public int Strength { get; set; }

        public int Dexterity { get; set; }

        public int Intelligence { get; set; }

        /// <summary>Ровно <see cref="ServerLimits.CreationSkillCount"/> пар (номер навыка, значение); значение 0 — пусто.</summary>
        public (int Skill, int Value)[] Skills { get; set; } = new (int, int)[ServerLimits.CreationSkillCount];

        /// <summary>Номер города в списке сервера (<see cref="StartingCity.Index"/>).</summary>
        public int CityIndex { get; set; }

        public int SkinHue { get; set; }

        public int HairStyle { get; set; }

        public int HairHue { get; set; }

        public int BeardStyle { get; set; }

        public int BeardHue { get; set; }

        public int ShirtHue { get; set; }

        public int PantsHue { get; set; }

        public override string ToString() => Name;
    }
}
