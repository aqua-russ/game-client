using System.Text;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Правила создания персонажа (docs/TARGET.md §4, числа — <see cref="ServerLimits"/>). Сервер неверный выбор
    /// не отвергает, а молча портит: статы — 10/10/10, навыки — без навыков, имя — «Generic Player»
    /// (UOContent/Engines/Character Creation/CharacterCreation.cs: SetStats, ValidateSkills, SetName). Поэтому клиент
    /// проверяет сам и не отправляет то, что сервер испортит. Полная проверка имени (запрещённые слова и символы,
    /// NameVerification сервера) — в окне создания, задача 1.16.
    /// </summary>
    public static class CharacterCreationRules
    {
        /// <summary>Поле имени в 0xF8 — 30 байт UTF-8 (IncomingAccountPackets.cs, CreateCharacter: ReadUTF8(30)).</summary>
        public const int NameMaxBytes = 30;

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

        private static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "пустое имя";
            }

            return Encoding.UTF8.GetByteCount(name) > NameMaxBytes ? "имя длиннее " + NameMaxBytes + " байт UTF-8" : string.Empty;
        }

        private static string ValidateStats(int str, int dex, int intel)
        {
            if (!InRange(str) || !InRange(dex) || !InRange(intel))
            {
                return "каждый стат — от " + ServerLimits.CreationStatMin + " до " + ServerLimits.CreationStatMax;
            }

            return str + dex + intel == ServerLimits.CreationStatTotal
                ? string.Empty
                : "сумма статов должна быть " + ServerLimits.CreationStatTotal + ", а не " + (str + dex + intel);
        }

        private static string ValidateSkills((int Skill, int Value)[] skills)
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
                return "навык " + skill + " нельзя выбрать при создании в эре UOR";
            }

            if (value < 0 || value > ServerLimits.CreationSkillMax)
            {
                return "навык " + skill + ": значение от 0 до " + ServerLimits.CreationSkillMax;
            }

            for (var j = i + 1; j < skills.Length; j++)
            {
                if (skills[j].Value > 0 && skills[j].Skill == skill)
                {
                    return "навык " + skill + " выбран дважды";
                }
            }

            return string.Empty;
        }

        private static bool IsAllowedSkill(int skill) =>
            (skill >= 0 && skill <= ServerLimits.CreationSkillLastClassic) || skill == ServerLimits.CreationSkillMysticism;

        private static bool InRange(int stat) => stat >= ServerLimits.CreationStatMin && stat <= ServerLimits.CreationStatMax;
    }
}
