using System.Text;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Правила создания персонажа (docs/TARGET.md §4, числа — <see cref="ServerLimits"/>). Сервер неверный выбор
    /// не отвергает, а молча портит: статы — 10/10/10, навыки — без навыков, имя — «Generic Player»
    /// (UOContent/Engines/Character Creation/CharacterCreation.cs: SetStats, ValidateSkills, SetName; имя —
    /// UOContent/Misc/NameVerification.cs, ValidatePlayerName). Поэтому клиент проверяет сам и не отправляет то, что
    /// сервер испортит.
    /// </summary>
    public static class CharacterCreationRules
    {
        /// <summary>Пусто — можно отправлять; иначе — что не так, словами.</summary>
        public static string Validate(NewCharacter character)
        {
            var problem = ValidateName(character.Name);
            if (problem.Length == 0)
            {
                problem = ValidateStats(character.Strength, character.Dexterity, character.Intelligence);
            }

            return problem.Length == 0 ? ValidateSkills(character.Skills) : problem;
        }

        /// <summary>Имя (как сервер: CharacterCreation.SetName обрезает пробелы по краям, затем ValidatePlayerName).</summary>
        public static string ValidateName(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length < ServerLimits.NameMinChars || trimmed.Length > ServerLimits.NameMaxChars)
            {
                return "имя — от " + ServerLimits.NameMinChars + " до " + ServerLimits.NameMaxChars + " символов";
            }

            if (Encoding.UTF8.GetByteCount(trimmed) > ServerLimits.NameMaxUtf8Bytes)
            {
                return "имя длиннее " + ServerLimits.NameMaxUtf8Bytes + " байт UTF-8 (кириллица — 2 байта на букву)";
            }

            foreach (var c in trimmed)
            {
                if (!char.IsLetter(c) && c != ' ' && c != '-' && c != '\'')
                {
                    return "имя: можно только буквы, пробел, дефис и апостроф";
                }
            }

            return string.Empty;
        }

        /// <summary>Три стата: каждый в пределах и сумма как у сервера.</summary>
        public static string ValidateStats(int str, int dex, int intel)
        {
            if (!InRange(str) || !InRange(dex) || !InRange(intel))
            {
                return "каждый стат — от " + ServerLimits.CreationStatMin + " до " + ServerLimits.CreationStatMax;
            }

            return str + dex + intel == ServerLimits.CreationStatTotal
                ? string.Empty
                : "сумма статов должна быть " + ServerLimits.CreationStatTotal + ", а не " + (str + dex + intel);
        }

        /// <summary>Навыки: число, разрешённые номера, значения, повторы и сумма.</summary>
        public static string ValidateSkills((int Skill, int Value)[] skills)
        {
            if (skills == null || skills.Length != ServerLimits.CreationSkillCount)
            {
                return "навыков должно быть " + ServerLimits.CreationSkillCount;
            }

            var total = 0;
            for (var i = 0; i < skills.Length; i++)
            {
                var problem = ValidateSkill(skills, i);
                if (problem.Length > 0)
                {
                    return problem;
                }

                total += skills[i].Value;
            }

            return total == ServerLimits.CreationSkillTotalLow || total == ServerLimits.CreationSkillTotalHigh
                ? string.Empty
                : "сумма навыков должна быть " + ServerLimits.CreationSkillTotalLow + " или " + ServerLimits.CreationSkillTotalHigh +
                  ", а не " + total;
        }

        // Как ValidateSkills сервера: и навык со значением 0 должен быть из разрешённых, повтор считается при значении > 0.
        private static string ValidateSkill((int Skill, int Value)[] skills, int i)
        {
            var (skill, value) = skills[i];
            if (!IsAllowedSkill(skill))
            {
                return "навык " + Describe(skill) + " нельзя выбрать при создании в эре UOR";
            }

            if (value < 0 || value > ServerLimits.CreationSkillMax)
            {
                return "навык " + Describe(skill) + ": значение от 0 до " + ServerLimits.CreationSkillMax;
            }

            for (var j = i + 1; j < skills.Length; j++)
            {
                if (skills[j].Value > 0 && skills[j].Skill == skill)
                {
                    return "навык " + Describe(skill) + " выбран дважды";
                }
            }

            return string.Empty;
        }

        /// <summary>Навык можно выбрать при создании (ValidateSkills сервера).</summary>
        public static bool IsAllowedSkill(int skill) =>
            (skill >= 0 && skill <= ServerLimits.CreationSkillLastClassic) || skill == ServerLimits.CreationSkillMysticism;

        private static string Describe(int skill) => SkillNames.Name(skill) + " (" + skill + ")";

        private static bool InRange(int stat) => stat >= ServerLimits.CreationStatMin && stat <= ServerLimits.CreationStatMax;
    }
}
