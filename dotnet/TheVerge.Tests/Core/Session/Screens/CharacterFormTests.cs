using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;

namespace TheVerge.Tests.Core.Session.Screens
{
    /// <summary>
    /// Окно создания персонажа (PLAN Стадия 1, п. 16): правила — TARGET.md §4 и ServerLimits (сервер:
    /// UOContent/Engines/Character Creation/CharacterCreation.cs), имена навыков — SkillName сервера
    /// (Projects/Server/Skills.cs). Кнопка «Создать» активна, только когда правила выполнены; под каждой частью —
    /// подсказка, что не так.
    /// </summary>
    public sealed class CharacterFormTests
    {
        private static readonly StartingCity[] OneCity = { new StartingCity(0, "Britain", "Inn", new Point3D(1, 2, 3), 0, 0) };

        [Test]
        public void SkillOptions_ExactlyCreationSkillsOfServer_WithServerNames()
        {
            var options = CharacterForm.SkillOptions;

            Assert.That(options.Count, Is.EqualTo(48), "Alchemy (0) … Meditation (46) и Mysticism (55)");
            Assert.That(options[0], Is.EqualTo(0));
            Assert.That(options[46], Is.EqualTo(46));
            Assert.That(options[47], Is.EqualTo(55));
            Assert.That(SkillNames.Name(2), Is.EqualTo("Animal Lore"));
            Assert.That(SkillNames.Name(25), Is.EqualTo("Magery"));
            Assert.That(SkillNames.Name(43), Is.EqualTo("Wrestling"));
            Assert.That(SkillNames.Name(46), Is.EqualTo("Meditation"));
            Assert.That(SkillNames.Name(55), Is.EqualTo("Mysticism"));
            Assert.That(SkillNames.Name(57), Is.EqualTo("Throwing"));
            Assert.That(SkillNames.Name(58), Is.EqualTo("навык 58"));
        }

        [Test]
        public void Default_OnlyNameMissing_StatsAndSkillsValid()
        {
            var form = new CharacterForm();
            form.SetCities(OneCity);

            Assert.That(form.NameHint, Is.EqualTo("Имя — от 2 до 24 символов."));
            Assert.That(form.StatsHint, Is.Empty);
            Assert.That(form.SkillsHint, Is.Empty);
            Assert.That(form.CityHint, Is.Empty);
            Assert.That(form.CanCreate, Is.False);

            form.Name = "Борис";
            Assert.That(form.CanCreate, Is.True);
        }

        [Test]
        public void Stats_WrongSum_HintShowsSum_ButtonDisabled()
        {
            var form = Valid();
            form.Strength = 29;

            Assert.That(form.StatsHint, Is.EqualTo("Сумма статов должна быть 90, а не 89."));
            Assert.That(form.StatsTotal, Is.EqualTo("Сумма: 89 из 90"));
            Assert.That(form.CanCreate, Is.False);
        }

        [Test]
        public void Stats_OutOfRange_Hint()
        {
            var form = Valid();
            form.Strength = 61;
            form.Dexterity = 19;
            form.Intelligence = 10;

            Assert.That(form.StatsHint, Is.EqualTo("Каждый стат — от 10 до 60."));
        }

        [Test]
        public void Skills_WrongSum_OrTwice_OrTooHigh_Hint()
        {
            var form = Valid();
            form.SetSkillValue(2, 10);
            Assert.That(form.SkillsHint, Is.EqualTo("Сумма навыков должна быть 100 или 120, а не 110."));
            Assert.That(form.SkillsTotal, Is.EqualTo("Сумма: 110 (нужно 100 или 120)"));

            form.SetSkillValue(2, 20);
            Assert.That(form.SkillsHint, Is.Empty, "120 — тоже можно");

            form.SetSkill(2, form.SkillOption(0));
            Assert.That(form.SkillsHint, Does.Contain("Alchemy (0)").And.Contain("дважды"));

            form.SetSkill(2, 3);
            form.SetSkillValue(0, 51);
            Assert.That(form.SkillsHint, Does.Contain("от 0 до 50"));
            Assert.That(form.CanCreate, Is.False);
        }

        [Test]
        public void City_NoneFromServer_Hint()
        {
            var form = Valid();
            form.SetCities(new StartingCity[0]);

            Assert.That(form.CityHint, Is.EqualTo("Сервер не прислал стартовых городов: создать персонажа негде."));
            Assert.That(form.CanCreate, Is.False);
        }

        [Test]
        public void ToNewCharacter_CarriesChoice_NameTrimmed()
        {
            var form = Valid();
            form.Name = "  Борис  ";
            form.Female = true;
            form.SetSkill(0, 25);
            form.SetSkillValue(0, 50);

            var c = form.ToNewCharacter();

            Assert.That(c.Name, Is.EqualTo("Борис"));
            Assert.That(c.Female, Is.True);
            Assert.That((c.Strength, c.Dexterity, c.Intelligence), Is.EqualTo((30, 30, 30)));
            Assert.That(c.Skills[0], Is.EqualTo((25, 50)));
            Assert.That(c.CityIndex, Is.EqualTo(0));
            Assert.That(CharacterCreationRules.Validate(c), Is.Empty);
        }

        [Test]
        public void Change_GrowsRevision()
        {
            var form = Valid();
            var before = form.Revision;

            form.SetSkillValue(1, 40);

            Assert.That(form.Revision, Is.GreaterThan(before));
        }

        [Test]
        public void Reset_BackToDefaults()
        {
            var form = Valid();
            form.Strength = 50;

            form.Reset();

            Assert.That(form.Name, Is.Empty);
            Assert.That(form.Strength, Is.EqualTo(30));
        }

        private static CharacterForm Valid()
        {
            var form = new CharacterForm { Name = "Борис" };
            form.SetCities(OneCity);
            return form;
        }
    }
}
