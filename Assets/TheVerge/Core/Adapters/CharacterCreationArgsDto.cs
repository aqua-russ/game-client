using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Параметры создания персонажа для отправки на сервер (пакет 0xF8).
    /// Строго соблюдает формулу GDD: 80 статов (10..60) и 3 навыка (50.0 / 50.0 / 10.0).
    /// </summary>
    public readonly struct CharacterCreationArgsDto
    {
        public string CharacterName { get; }
        public bool Female { get; }
        public byte Strength { get; }
        public byte Dexterity { get; }
        public byte Intelligence { get; }
        public SkillType Skill1 { get; }
        public byte Skill1Value { get; }
        public SkillType Skill2 { get; }
        public byte Skill2Value { get; }
        public SkillType Skill3 { get; }
        public byte Skill3Value { get; }
        public ushort SkinHue { get; }
        public ushort ShirtHue { get; }
        public ushort PantsHue { get; }

        public CharacterCreationArgsDto(
            string name,
            bool female,
            byte str,
            byte dex,
            byte intel,
            SkillType skill1,
            byte skill1Val,
            SkillType skill2,
            byte skill2Val,
            SkillType skill3,
            byte skill3Val,
            ushort skinHue = 0x03EA,
            ushort shirtHue = 0x0455,
            ushort pantsHue = 0x0481)
        {
            CharacterName = string.IsNullOrWhiteSpace(name) ? "Ратибор" : name.Trim();
            Female = female;
            Strength = str;
            Dexterity = dex;
            Intelligence = intel;
            Skill1 = skill1;
            Skill1Value = skill1Val;
            Skill2 = skill2;
            Skill2Value = skill2Val;
            Skill3 = skill3;
            Skill3Value = skill3Val;
            SkinHue = skinHue;
            ShirtHue = shirtHue;
            PantsHue = pantsHue;
        }

        public static CharacterCreationArgsDto CreateFromPreset(CharacterPresetType preset, string name, bool female = false)
        {
            return preset switch
            {
                CharacterPresetType.Warrior => new CharacterCreationArgsDto(
                    name, female, 50, 20, 10,
                    SkillType.Swords, 50,
                    SkillType.Parry, 50,
                    SkillType.Tactics, 0),

                CharacterPresetType.Mage => new CharacterCreationArgsDto(
                    name, female, 30, 10, 40,
                    SkillType.Magery, 50,
                    SkillType.Meditation, 30,
                    SkillType.MagicResist, 20),

                CharacterPresetType.LumberjackCarpenter => new CharacterCreationArgsDto(
                    name, female, 50, 20, 10,
                    SkillType.Lumberjacking, 50,
                    SkillType.Carpentry, 50,
                    SkillType.Swords, 0),

                CharacterPresetType.Archer => new CharacterCreationArgsDto(
                    name, female, 40, 30, 10,
                    SkillType.Archery, 50,
                    SkillType.Fletching, 30,
                    SkillType.Tactics, 20),

                CharacterPresetType.Alchemist => new CharacterCreationArgsDto(
                    name, female, 30, 10, 40,
                    SkillType.Alchemy, 50,
                    SkillType.Cooking, 30,
                    SkillType.Magery, 20),

                _ => new CharacterCreationArgsDto(
                    name, female, 50, 20, 10,
                    SkillType.Mining, 50,
                    SkillType.Blacksmith, 50,
                    SkillType.Swords, 0)
            };
        }
    }
}
