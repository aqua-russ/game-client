using System.Collections.Generic;

namespace TheVerge.Core
{
    /// <summary>
    /// Описание активных навыков игры The Verge (устаревшее: список GDD) с русскими названиями.
    /// Соответствует реестру 27 дисциплин из docs/gdd/02-skills.md.
    /// </summary>
    public static class SkillInfo
    {
        public readonly struct Entry
        {
            public SkillType Type { get; }
            public string NameRu { get; }
            public string Category { get; }

            public Entry(SkillType type, string nameRu, string category)
            {
                Type = type;
                NameRu = nameRu;
                Category = category;
            }
        }

        public static readonly IReadOnlyList<Entry> AllSkills = new Entry[]
        {
            // Бой и оружие
            new Entry(SkillType.Swords, "Мечи и топоры", "Бой"),
            new Entry(SkillType.Macing, "Дробящее оружие", "Бой"),
            new Entry(SkillType.Fencing, "Фехтование и копья", "Бой"),
            new Entry(SkillType.Archery, "Стрельба из лука", "Бой"),
            new Entry(SkillType.Tactics, "Тактика", "Бой"),
            new Entry(SkillType.Anatomy, "Анатомия", "Бой"),
            new Entry(SkillType.Parry, "Парирование щитом", "Бой"),
            new Entry(SkillType.Healing, "Лечение бинтами", "Бой"),

            // Магия и дух
            new Entry(SkillType.Magery, "Классическая магия", "Магия"),
            new Entry(SkillType.Meditation, "Медитация", "Магия"),
            new Entry(SkillType.MagicResist, "Сопротивление магии", "Магия"),
            new Entry(SkillType.Inscribe, "Начертание свитков", "Магия"),

            // Добыча и ремёсла
            new Entry(SkillType.Mining, "Горное дело", "Ремесло"),
            new Entry(SkillType.Blacksmith, "Кузнечное дело", "Ремесло"),
            new Entry(SkillType.Lumberjacking, "Лесозаготовка", "Ремесло"),
            new Entry(SkillType.Carpentry, "Плотничество", "Ремесло"),
            new Entry(SkillType.Fletching, "Изготовление луков", "Ремесло"),
            new Entry(SkillType.Tailoring, "Портняжное дело", "Ремесло"),
            new Entry(SkillType.Tinkering, "Слесарное дело", "Ремесло"),
            new Entry(SkillType.Alchemy, "Алхимия и Яды", "Ремесло"),
            new Entry(SkillType.Cooking, "Кулинария", "Ремесло"),
            new Entry(SkillType.Fishing, "Рыбная ловля", "Ремесло"),

            // Выживание и плутовство
            new Entry(SkillType.Hiding, "Скрытность", "Выживание"),
            new Entry(SkillType.Stealing, "Воровство", "Выживание"),
            new Entry(SkillType.Lockpicking, "Взлом замков", "Выживание"),
            new Entry(SkillType.AnimalTaming, "Приручение зверей", "Выживание"),
            new Entry(SkillType.AnimalLore, "Знание животных", "Выживание")
        };

        public static string GetNameRu(SkillType type)
        {
            for (int i = 0; i < AllSkills.Count; i++)
            {
                if (AllSkills[i].Type == type) return AllSkills[i].NameRu;
            }
            return type.ToString();
        }

        public static string GetStarterKitDescription(SkillType type)
        {
            return type switch
            {
                SkillType.Mining => "Вечная кирка рудокопа",
                SkillType.Blacksmith => "Вечный молот + 15 слитков",
                SkillType.Lumberjacking => "Вечный топор лесоруба",
                SkillType.Carpentry => "Вечная пила + 15 досок",
                SkillType.Fletching => "Вечный нож стрельника + припасы",
                SkillType.Tinkering => "Вечный набор инструментов + слитки",
                SkillType.Tailoring => "Вечный набор шитья + ткань, кожа",
                SkillType.Alchemy => "Вечная ступка + склянки, реагенты",
                SkillType.Cooking => "Вечная сковорода + мясо, мука",
                SkillType.Fishing => "Вечная удочка",
                SkillType.Swords => "Вечный учебный меч",
                SkillType.Macing => "Вечная учебная булава",
                SkillType.Fencing => "Вечное учебное копьё",
                SkillType.Archery => "Вечный учебный лук + 50 стрел",
                SkillType.Parry => "Вечный учебный щит",
                SkillType.Healing => "Запас бинтов (+30 шт.)",
                SkillType.Magery => "Книга заклинаний + 15 реагентов",
                SkillType.Inscribe => "Вечное перо + 10 свитков",
                SkillType.AnimalTaming => "Вечный пастуший посох + прикорм",
                SkillType.Lockpicking => "Запас отмычек (10 шт.)",
                _ => "Походная накидка + 50 золота"
            };
        }
    }
}
