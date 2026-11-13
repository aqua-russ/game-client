using System;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Список персонажей (PLAN Стадия 1, п. 8). Сервер после входа на игровой сервер шлёт флаги возможностей и список
    /// (IncomingAccountPackets.cs, GameLogin → SendSupportedFeature, SendCharacterList); выбор персонажа — номер слота
    /// (PlayCharacter: неверный или пустой слот — отключение).
    /// </summary>
    public sealed class CharacterSelectionTests
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
            _sender.Calls.Clear();
        }

        [Test]
        public void SupportedFeatures_Stored()
        {
            _flow.Characters.OnSupportedFeatures(0x8000_02DF);

            Assert.That(_flow.Characters.SupportedFeatures, Is.EqualTo(0x8000_02DFu));
        }

        [Test]
        public void CharacterList_StoredAndAwaitsChoice()
        {
            _flow.Characters.OnCharacterList(Slots(), Cities(), 0x14);

            Assert.That(_flow.State, Is.EqualTo(LoginState.CharacterSelection));
            Assert.That(_flow.Characters.Slots.Count, Is.EqualTo(5));
            Assert.That(_flow.Characters.Slots[1].Name, Is.EqualTo("Борис"));
            Assert.That(_flow.Characters.Slots[0].IsEmpty, Is.True);
            Assert.That(_flow.Characters.Cities[0].City, Is.EqualTo("Britain"));
            Assert.That(_flow.Characters.ListFlags, Is.EqualTo(0x14));
            Assert.That(_sender.Calls, Is.Empty);
        }

        [Test]
        public void CharacterList_BeforeGameLogin_Ignored()
        {
            var flow = new LoginFlow(_sender, new RecordingConnector(), 1);

            flow.Characters.OnCharacterList(Slots(), Cities(), 0);

            Assert.That(flow.State, Is.EqualTo(LoginState.Idle));
            Assert.That(flow.Characters.Slots, Is.Empty);
        }

        [Test]
        public void Select_OccupiedSlot_SendsSlotAndName()
        {
            _flow.Characters.OnCharacterList(Slots(), Cities(), 0);

            _flow.Characters.Select(3);

            Assert.That(_sender.Calls, Is.EqualTo(new[] { "play" }));
            Assert.That(_sender.CharacterSlot, Is.EqualTo(3));
            Assert.That(_sender.CharacterName, Is.EqualTo("Lord British"));
            Assert.That(_flow.State, Is.EqualTo(LoginState.EnteringWorld));
        }

        [Test]
        public void Select_EmptySlot_RefusedNothingSent()
        {
            _flow.Characters.OnCharacterList(Slots(), Cities(), 0);

            Assert.Throws<ArgumentException>(new Action(() => _flow.Characters.Select(0)));
            Assert.That(_sender.Calls, Is.Empty);
            Assert.That(_flow.State, Is.EqualTo(LoginState.CharacterSelection));
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void Select_OutsideList_RefusedNothingSent(int slot)
        {
            _flow.Characters.OnCharacterList(Slots(), Cities(), 0);

            Assert.Throws<ArgumentOutOfRangeException>(new Action(() => _flow.Characters.Select(slot)));
            Assert.That(_sender.Calls, Is.Empty);
        }

        [Test]
        public void Select_BeforeListOrTwice_Refused()
        {
            Assert.Throws<InvalidOperationException>(new Action(() => _flow.Characters.Select(1)));
            _flow.Characters.OnCharacterList(Slots(), Cities(), 0);
            _flow.Characters.Select(1);

            Assert.Throws<InvalidOperationException>(new Action(() => _flow.Characters.Select(1)));
            Assert.That(_sender.Calls, Is.EqualTo(new[] { "play" }));
        }

        [Test]
        public void FindSlot_ByNameIgnoringCase_OrMinusOne()
        {
            _flow.Characters.OnCharacterList(Slots(), Cities(), 0);

            Assert.That(_flow.Characters.FindSlot("lord british"), Is.EqualTo(3));
            Assert.That(_flow.Characters.FindSlot("борис"), Is.EqualTo(1));
            Assert.That(_flow.Characters.FindSlot("Nobody"), Is.EqualTo(-1));
            Assert.That(_flow.Characters.FindSlot(string.Empty), Is.EqualTo(-1), "пустое имя — не пустой слот");
        }

        private static CharacterSlot[] Slots() => new[]
        {
            new CharacterSlot(0, string.Empty),
            new CharacterSlot(1, "Борис"),
            new CharacterSlot(2, string.Empty),
            new CharacterSlot(3, "Lord British"),
            new CharacterSlot(4, string.Empty),
        };

        private static StartingCity[] Cities() => new[]
        {
            new StartingCity(0, "Britain", "The Wayfarer's Inn", new Point3D(1602, 1591, 20), 0, 1075074),
        };
    }
}
