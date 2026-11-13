using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;

namespace TheVerge.Tests.Core.Session.Screens
{
    /// <summary>
    /// Экран персонажей и окно создания (PLAN Стадия 1, п. 16): какие кнопки когда активны, удаление с подтверждением,
    /// отказ сервера в удалении словами (0x85; UOContent/Accounting/AccountHandler.cs, DeleteRequest: отказ — 0x85 и
    /// новый список, удачно — только новый список).
    /// </summary>
    public sealed class CharacterScreensTests
    {
        [Test]
        public void NothingSelected_EnterAndDeleteDisabled_CreateEnabledWithFreeSlot()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис", string.Empty);
            var list = h.Screens.Characters;

            Assert.That(list.Slots.Count, Is.EqualTo(2));
            Assert.That(list.CanEnter, Is.False);
            Assert.That(list.CanDelete, Is.False);
            Assert.That(list.CanCreate, Is.True);

            list.Select(1);
            Assert.That(list.CanEnter, Is.False, "пустой слот");

            list.Select(0);
            Assert.That(list.CanEnter, Is.True);
            Assert.That(list.CanDelete, Is.True);
        }

        [Test]
        public void NoFreeSlot_CreateDisabledWithHint()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис");

            Assert.That(h.Screens.Characters.CanCreate, Is.False);
            Assert.That(h.Screens.Characters.CreateHint, Is.EqualTo("Свободных слотов нет: чтобы создать персонажа, удалите одного."));
        }

        [Test]
        public void Delete_AsksFirst_CancelSendsNothing()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис", string.Empty);
            var list = h.Screens.Characters;
            list.Select(0);

            list.AskDelete();
            Assert.That(list.IsConfirmingDelete, Is.True);
            Assert.That(list.ConfirmText, Is.EqualTo("Удалить персонажа «Борис»? Вернуть его будет нельзя."));
            Assert.That(list.CanEnter, Is.False, "пока висит вопрос, другие кнопки неактивны");

            list.CancelDelete();
            Assert.That(list.IsConfirmingDelete, Is.False);
            Assert.That(h.Last.Account.Calls, Does.Not.Contain("delete"));
        }

        [Test]
        public void Delete_Confirmed_SentOnce_WaitsForServer_ThenDeletedMessage()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис", "Глеб");
            var list = h.Screens.Characters;
            list.Select(1);
            list.AskDelete();

            list.ConfirmDelete();
            Assert.That(h.Last.Account.DeletedSlot, Is.EqualTo(1));
            Assert.That(list.IsDeleting, Is.True);
            Assert.That(list.CanDelete, Is.False);
            Assert.That(list.CanEnter, Is.False);

            h.Last.Flow.Characters.OnCharacterListUpdate(new[] { new CharacterSlot(0, "Борис"), new CharacterSlot(1, string.Empty) });
            h.Tick();

            Assert.That(list.IsDeleting, Is.False);
            Assert.That(list.Message, Is.EqualTo("Персонаж «Глеб» удалён."));
            Assert.That(list.Selected, Is.EqualTo(-1), "после нового списка выбор сброшен");
            Assert.That(list.CanCreate, Is.True);
        }

        [Test]
        public void Delete_RejectedByServer_ReasonInWords()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис", string.Empty);
            var list = h.Screens.Characters;
            list.Select(0);
            list.AskDelete();
            list.ConfirmDelete();

            h.Last.Flow.Characters.OnDeleteRejected(CharacterDeleteResult.CharBeingPlayed);
            h.Last.Flow.Characters.OnCharacterListUpdate(new[] { new CharacterSlot(0, "Борис"), new CharacterSlot(1, string.Empty) });
            h.Tick();

            Assert.That(list.Message, Is.EqualTo("Сервер не удалил персонажа «Борис»: персонаж сейчас в игре."));
            Assert.That(list.IsDeleting, Is.False);
        }

        [Test]
        public void Create_OpenWindow_ButtonFollowsRules_CreateEntersWorld()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис", string.Empty);

            h.Screens.OpenCreation();
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.CreateCharacter));
            var form = h.Screens.Creation;
            Assert.That(form.CanCreate, Is.False, "имени ещё нет");

            h.Screens.Create();
            Assert.That(h.Last.Account.Calls, Does.Not.Contain("create"));

            form.Name = "Глеб";
            h.Screens.Create();

            Assert.That(h.Last.Account.Created!.Name, Is.EqualTo("Глеб"));
            Assert.That(h.Last.Account.CharacterSlot, Is.EqualTo(1));
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Waiting));
            Assert.That(h.Screens.Status, Is.EqualTo("Вход в мир…"));
        }

        [Test]
        public void Create_Cancel_BackToCharacters_FormResetNextTime()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис", string.Empty);
            h.Screens.OpenCreation();
            h.Screens.Creation.Name = "Глеб";

            h.Screens.CloseCreation();
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Characters));

            h.Screens.OpenCreation();
            Assert.That(h.Screens.Creation.Name, Is.Empty);
        }

        [Test]
        public void OpenCreation_NoFreeSlot_Ignored()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис");

            h.Screens.OpenCreation();

            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Characters));
        }
    }
}
