using System;
using System.Collections.Generic;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>
    /// Список персонажей: слоты кнопками (выбор), «Войти в мир», «Создать», «Удалить» с подтверждением, «Назад».
    /// Когда какая кнопка активна и что написать — решает <see cref="CharacterList"/>.
    /// </summary>
    public sealed class CharactersPanelView : IDisposable
    {
        private readonly LoginScreens _screens;
        private readonly CharacterList _characters;
        private readonly ButtonList _list;
        private readonly Label _message;
        private readonly Label _createHint;
        private readonly Button _enter;
        private readonly Button _create;
        private readonly Button _delete;
        private readonly Button _back;
        private readonly VisualElement _confirm;
        private readonly Label _confirmText;
        private readonly Button _confirmYes;
        private readonly Button _confirmNo;
        private IReadOnlyList<CharacterSlot> _shown;

        public CharactersPanelView(VisualElement root, LoginScreens screens)
        {
            _screens = screens;
            _characters = screens.Characters;
            Panel = UiElements.Require<VisualElement>(root, "charactersPanel");
            _list = new ButtonList(UiElements.Require<ScrollView>(Panel, "charactersList"), _characters.Select);
            _message = UiElements.Require<Label>(Panel, "charactersMessage");
            _createHint = UiElements.Require<Label>(Panel, "createHint");
            _enter = UiElements.Require<Button>(Panel, "enterButton");
            _create = UiElements.Require<Button>(Panel, "createButton");
            _delete = UiElements.Require<Button>(Panel, "deleteButton");
            _back = UiElements.Require<Button>(Panel, "charactersBack");
            _confirm = UiElements.Require<VisualElement>(Panel, "deleteConfirm");
            _confirmText = UiElements.Require<Label>(Panel, "deleteConfirmText");
            _confirmYes = UiElements.Require<Button>(Panel, "deleteYes");
            _confirmNo = UiElements.Require<Button>(Panel, "deleteNo");
            Subscribe(true);
        }

        public VisualElement Panel { get; }

        public void Refresh()
        {
            var slots = _characters.Slots;
            if (!ReferenceEquals(slots, _shown))
            {
                _shown = slots;
                _list.Rebuild(slots.Count, i => slots[i].IsEmpty ? "(пустой слот)" : slots[i].Name, i => slots[i].IsEmpty);
            }

            _list.SetSelected(_characters.Selected);
            _list.SetEnabled(!_characters.IsConfirmingDelete && !_characters.IsDeleting);
            UiElements.SetText(_message, _characters.IsDeleting ? "Удаление…" : _characters.Message);
            UiElements.SetText(_createHint, _characters.CreateHint);
            _enter.SetEnabled(_characters.CanEnter);
            _create.SetEnabled(_characters.CanCreate);
            _delete.SetEnabled(_characters.CanDelete);
            _back.SetEnabled(!_characters.IsDeleting);
            UiElements.Show(_confirm, _characters.IsConfirmingDelete);
            UiElements.SetText(_confirmText, _characters.ConfirmText);
        }

        public void Dispose()
        {
            Subscribe(false);
            _list.Dispose();
        }

        private void Subscribe(bool on)
        {
            if (on)
            {
                _enter.clicked += _characters.Enter;
                _create.clicked += _screens.OpenCreation;
                _delete.clicked += _characters.AskDelete;
                _back.clicked += _screens.Cancel;
                _confirmYes.clicked += _characters.ConfirmDelete;
                _confirmNo.clicked += _characters.CancelDelete;
                return;
            }

            _enter.clicked -= _characters.Enter;
            _create.clicked -= _screens.OpenCreation;
            _delete.clicked -= _characters.AskDelete;
            _back.clicked -= _screens.Cancel;
            _confirmYes.clicked -= _characters.ConfirmDelete;
            _confirmNo.clicked -= _characters.CancelDelete;
        }
    }
}
