using System;
using System.Collections.Generic;

namespace TheVerge.Core.Session.Screens
{
    /// <summary>
    /// Окно создания персонажа: выбор игрока и подсказки под каждой частью (имя, статы, навыки, город). Правила — только
    /// <see cref="CharacterCreationRules"/> (TARGET.md §4, числа — <see cref="ServerLimits"/>); здесь их не повторяем.
    /// </summary>
    public sealed class CharacterForm
    {
        private const int DefaultStat = ServerLimits.CreationStatTotal / 3;
        private static readonly int[] Options = AllowedSkills();
        private readonly (int Skill, int Value)[] _skills = new (int, int)[ServerLimits.CreationSkillCount];
        private IReadOnlyList<StartingCity> _cities = Array.Empty<StartingCity>();
        private string _name = string.Empty;
        private bool _female;
        private int _strength;
        private int _dexterity;
        private int _intelligence;
        private int _city;

        public CharacterForm()
        {
            Reset();
        }

        /// <summary>Номера навыков, которые можно выбрать при создании, по порядку номеров сервера.</summary>
        public static IReadOnlyList<int> SkillOptions => Options;

        public string Name { get => _name; set => Set(ref _name, value ?? string.Empty); }

        public bool Female { get => _female; set => Set(ref _female, value); }

        public int Strength { get => _strength; set => Set(ref _strength, value); }

        public int Dexterity { get => _dexterity; set => Set(ref _dexterity, value); }

        public int Intelligence { get => _intelligence; set => Set(ref _intelligence, value); }

        /// <summary>Место города в списке сервера (<see cref="Cities"/>).</summary>
        public int City { get => _city; set => Set(ref _city, value); }

        public IReadOnlyList<StartingCity> Cities => _cities;

        /// <summary>Растёт при каждой правке: экрану пора обновить подсказки и кнопку.</summary>
        public int Revision { get; private set; }

        public string NameHint => Sentence(CharacterCreationRules.ValidateName(_name));

        public string StatsHint => Sentence(CharacterCreationRules.ValidateStats(_strength, _dexterity, _intelligence));

        public string SkillsHint => Sentence(CharacterCreationRules.ValidateSkills(_skills));

        public string CityHint =>
            _cities.Count == 0 ? "Сервер не прислал стартовых городов: создать персонажа негде."
            : _city < 0 || _city >= _cities.Count ? "Выберите стартовый город."
            : string.Empty;

        public string StatsTotal => "Сумма: " + (_strength + _dexterity + _intelligence) + " из " + ServerLimits.CreationStatTotal;

        public string SkillsTotal => "Сумма: " + SkillSum() + " (нужно " + ServerLimits.CreationSkillTotalLow + " или " +
                                     ServerLimits.CreationSkillTotalHigh + ")";

        public bool CanCreate => NameHint.Length == 0 && StatsHint.Length == 0 && SkillsHint.Length == 0 && CityHint.Length == 0;

        /// <summary>Города из списка сервера; выбран первый.</summary>
        public void SetCities(IReadOnlyList<StartingCity> cities)
        {
            _cities = cities;
            _city = 0;
            Revision++;
        }

        /// <summary>Номер навыка в строке <paramref name="row"/>.</summary>
        public int SkillOption(int row) => _skills[row].Skill;

        public int SkillValue(int row) => _skills[row].Value;

        public void SetSkill(int row, int skill)
        {
            _skills[row].Skill = skill;
            Revision++;
        }

        public void SetSkillValue(int row, int value)
        {
            _skills[row].Value = value;
            Revision++;
        }

        /// <summary>Начальный выбор: без имени, статы поровну, первые навыки списка по 50 (сумма 100).</summary>
        public void Reset()
        {
            _name = string.Empty;
            _female = false;
            _strength = _dexterity = _intelligence = DefaultStat;
            for (var i = 0; i < _skills.Length; i++)
            {
                _skills[i] = (Options[i], i < 2 ? ServerLimits.CreationSkillMax : 0);
            }

            _city = 0;
            Revision++;
        }

        /// <summary>Что уйдёт серверу. Имя без пробелов по краям: так его сохранит и сервер.</summary>
        public NewCharacter ToNewCharacter() => new NewCharacter
        {
            Name = _name.Trim(),
            Female = _female,
            Strength = _strength,
            Dexterity = _dexterity,
            Intelligence = _intelligence,
            Skills = ((int, int)[])_skills.Clone(),
            CityIndex = _city,
        };

        private static string Sentence(string problem) =>
            problem.Length == 0 ? string.Empty : char.ToUpperInvariant(problem[0]) + problem.Substring(1) + ".";

        private static int[] AllowedSkills()
        {
            var allowed = new List<int>();
            for (var skill = 0; skill < SkillNames.Count; skill++)
            {
                if (CharacterCreationRules.IsAllowedSkill(skill))
                {
                    allowed.Add(skill);
                }
            }

            return allowed.ToArray();
        }

        private int SkillSum()
        {
            var sum = 0;
            foreach (var (_, value) in _skills)
            {
                sum += value;
            }

            return sum;
        }

        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            Revision++;
        }
    }
}
