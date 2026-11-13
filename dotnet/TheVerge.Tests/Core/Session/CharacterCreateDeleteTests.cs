using System;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Создание и удаление персонажа (PLAN Стадия 1, п. 8; правила — TARGET.md §4). Сервер неверное создание не отвергает,
    /// а портит молча (UOContent/Engines/Character Creation/CharacterCreation.cs: SetStats → 10/10/10, ValidateSkills →
    /// без навыков), поэтому клиент такое не отправляет. Удаление: AccountHandler.DeleteRequest — отказ 0x85 с причиной и
    /// новый список 0x86, удачно — только новый список.
    /// </summary>
    public sealed class CharacterCreateDeleteTests
    {
        private RecordingAccountSender _sender = new RecordingAccountSender();
        private LoginFlow _flow = null!;

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingAccountSender();
            _flow = new LoginFlow(_sender, new RecordingConnector(), 1);
            _flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            _flow.SelectShard(0);
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            _flow.OnConnected();
            _flow.Characters.OnCharacterList(
                new[] { new CharacterSlot(0, "Борис"), new CharacterSlot(1, string.Empty), new CharacterSlot(2, string.Empty) },
                new[] { new StartingCity(0, "Britain", "Inn", new Point3D(1, 2, 3), 0, 0) },
                0);
            _sender.Calls.Clear();
        }

        [Test]
        public void Rules_ValidCharacter_NoProblem()
        {
            Assert.That(CharacterCreationRules.Validate(Valid()), Is.Empty);
        }

        [TestCase(30, 30, 31, "90")]
        [TestCase(9, 41, 40, "10")]
        [TestCase(61, 19, 10, "60")]
        public void Rules_Stats_SumAndRange(int str, int dex, int intel, string mentioned)
        {
            var c = Valid();
            c.Strength = str;
            c.Dexterity = dex;
            c.Intelligence = intel;

            Assert.That(CharacterCreationRules.Validate(c), Does.Contain(mentioned));
        }

        [Test]
        public void Rules_Skills_SumOf120Allowed_OtherSumsNot()
        {
            var c = Valid();
            c.Skills = new[] { (25, 50), (26, 50), (16, 20), (0, 0) };
            Assert.That(CharacterCreationRules.Validate(c), Is.Empty);

            c.Skills = new[] { (25, 50), (26, 40), (0, 0), (1, 0) };
            Assert.That(CharacterCreationRules.Validate(c), Does.Contain("100").And.Contain("120"));
        }

        [Test]
        public void Rules_Skills_Over50_DuplicatesAndUorForbidden_Refused()
        {
            var c = Valid();
            c.Skills = new[] { (25, 51), (26, 49), (0, 0), (1, 0) };
            Assert.That(CharacterCreationRules.Validate(c), Does.Contain("50"));

            c.Skills = new[] { (25, 50), (25, 50), (0, 0), (1, 0) };
            Assert.That(CharacterCreationRules.Validate(c), Does.Contain("дважды"));

            foreach (var forbidden in new[] { 47, 48, 49, 50, 51, 52, 53, 54, 56, 57, 58, -1 })
            {
                c.Skills = new[] { (forbidden, 50), (26, 50), (0, 0), (1, 0) };
                Assert.That(CharacterCreationRules.Validate(c), Does.Contain(forbidden.ToString()), "навык " + forbidden);
            }

            c.Skills = new[] { (55, 50), (26, 50), (0, 0), (1, 0) };
            Assert.That(CharacterCreationRules.Validate(c), Is.Empty, "Mysticism (55) сервер при создании разрешает");
        }

        [Test]
        public void Rules_Name_EmptyOrLongerThan30BytesUtf8_Refused()
        {
            var c = Valid();
            c.Name = "  ";
            Assert.That(CharacterCreationRules.Validate(c), Does.Contain("имя"));

            c.Name = new string('Ж', 16); // 32 байта UTF-8 — не влезет в поле 30 байт
            Assert.That(CharacterCreationRules.Validate(c), Does.Contain("30"));
        }

        [Test]
        public void Rules_SkillsNotFour_Refused()
        {
            var c = Valid();
            c.Skills = new[] { (25, 50), (26, 50) };

            Assert.That(CharacterCreationRules.Validate(c), Does.Contain("4"));
        }

        [Test]
        public void Create_Valid_SentWithFirstFreeSlot_EnteringWorld()
        {
            var c = Valid();

            _flow.Characters.Create(c);

            Assert.That(_sender.Calls, Is.EqualTo(new[] { "create" }));
            Assert.That(_sender.Created, Is.SameAs(c));
            Assert.That(_sender.CharacterSlot, Is.EqualTo(1));
            Assert.That(_flow.State, Is.EqualTo(LoginState.EnteringWorld));
        }

        [Test]
        public void Create_Invalid_RefusedNothingSent()
        {
            var c = Valid();
            c.Strength = 70;

            Assert.Throws<ArgumentException>(new Action(() => _flow.Characters.Create(c)));
            Assert.That(_sender.Calls, Is.Empty);
            Assert.That(_flow.State, Is.EqualTo(LoginState.CharacterSelection));
        }

        [Test]
        public void Create_UnknownCity_Refused()
        {
            // Город не из списка сервер наказывает отключением (CreateCharacter: cityIndex >= info.Length).
            var c = Valid();
            c.CityIndex = 1;

            Assert.Throws<ArgumentException>(new Action(() => _flow.Characters.Create(c)));
            Assert.That(_sender.Calls, Is.Empty);
        }

        [Test]
        public void Create_NoFreeSlot_Refused()
        {
            _flow.Characters.OnCharacterListUpdate(new[] { new CharacterSlot(0, "A"), new CharacterSlot(1, "B") });

            Assert.Throws<InvalidOperationException>(new Action(() => _flow.Characters.Create(Valid())));
            Assert.That(_sender.Calls, Is.Empty);
        }

        [Test]
        public void Delete_OccupiedSlot_Sent_ListUpdateReplacesSlots()
        {
            var changed = 0;
            _flow.Characters.SlotsChanged += () => changed++;

            _flow.Characters.Delete(0);
            _flow.Characters.OnCharacterListUpdate(new[] { new CharacterSlot(0, string.Empty), new CharacterSlot(1, string.Empty) });

            Assert.That(_sender.Calls, Is.EqualTo(new[] { "delete" }));
            Assert.That(_sender.DeletedSlot, Is.EqualTo(0));
            Assert.That(_flow.Characters.Slots[0].IsEmpty, Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(_flow.State, Is.EqualTo(LoginState.CharacterSelection));
        }

        [Test]
        public void Delete_Rejected_TypedReason_StillSelecting()
        {
            CharacterDeleteResult? reason = null;
            _flow.Characters.DeleteRejected += r => reason = r;
            _flow.Characters.Delete(0);

            _flow.Characters.OnDeleteRejected(CharacterDeleteResult.CharTooYoung);

            Assert.That(reason, Is.EqualTo(CharacterDeleteResult.CharTooYoung));
            Assert.That(_flow.State, Is.EqualTo(LoginState.CharacterSelection));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
        }

        [Test]
        public void Delete_EmptyOrOutsideSlot_Refused()
        {
            Assert.Throws<ArgumentException>(new Action(() => _flow.Characters.Delete(1)));
            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => _flow.Characters.Delete(3)));
            Assert.That(_sender.Calls, Is.Empty);
        }

        private static NewCharacter Valid() => new NewCharacter
        {
            Name = "Борис",
            Strength = 50,
            Dexterity = 30,
            Intelligence = 10,
            Skills = new[] { (25, 50), (26, 30), (16, 20), (0, 0) },
            CityIndex = 0,
        };
    }
}
