using System;
using System.Collections.Generic;

namespace TheVerge.Core.Session.Screens
{
    /// <summary>
    /// Экран персонажей: выбранный слот, какие кнопки активны, удаление с подтверждением и ответ сервера на удаление.
    /// Решения о протоколе — в <see cref="CharacterSelection"/>; здесь — только когда его звать.
    /// </summary>
    public sealed class CharacterList
    {
        private LoginFlow? _flow;
        private string _deletingName = string.Empty;
        private bool _deleteRejected;

        /// <summary>Слоты сервера вместе с пустыми; пусто, пока нет списка.</summary>
        public IReadOnlyList<CharacterSlot> Slots => _flow?.Characters.Slots ?? Array.Empty<CharacterSlot>();

        /// <summary>Выбранный слот; -1 — не выбран.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>Висит вопрос «удалить?» о выбранном персонаже.</summary>
        public bool IsConfirmingDelete { get; private set; }

        /// <summary>Удаление отправлено, ждём новый список от сервера.</summary>
        public bool IsDeleting { get; private set; }

        /// <summary>Итог последнего удаления словами; пусто — нечего сказать.</summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>Растёт при каждом изменении того, что видно на экране.</summary>
        public int Revision { get; private set; }

        public bool CanEnter => IsIdle && IsOccupied(Selected);

        public bool CanDelete => CanEnter;

        public bool CanCreate => IsIdle && CreateHint.Length == 0;

        /// <summary>Почему создать нельзя; пусто — можно.</summary>
        public string CreateHint =>
            FreeSlot() < 0 ? "Свободных слотов нет: чтобы создать персонажа, удалите одного."
            : _flow == null || _flow.Characters.Cities.Count == 0 ? "Сервер не прислал стартовых городов: создать персонажа негде."
            : string.Empty;

        public string ConfirmText => IsOccupied(Selected)
            ? "Удалить персонажа «" + Slots[Selected].Name + "»? Вернуть его будет нельзя."
            : string.Empty;

        private bool IsIdle => _flow != null && _flow.State == LoginState.CharacterSelection && !IsConfirmingDelete && !IsDeleting;

        /// <summary>Новый клиент: подписаться на его список; прежний забыт.</summary>
        public void Attach(LoginFlow flow)
        {
            Detach();
            _flow = flow;
            flow.Characters.SlotsChanged += OnSlotsChanged;
            flow.Characters.DeleteRejected += OnDeleteRejected;
        }

        public void Detach()
        {
            if (_flow != null)
            {
                _flow.Characters.SlotsChanged -= OnSlotsChanged;
                _flow.Characters.DeleteRejected -= OnDeleteRejected;
                _flow = null;
            }

            Clear();
        }

        /// <summary>Новый список с сервера (вход в список): выбор и сообщения сброшены.</summary>
        public void Clear()
        {
            Selected = -1;
            IsConfirmingDelete = false;
            IsDeleting = false;
            Message = string.Empty;
            Revision++;
        }

        public void Select(int slot)
        {
            if (!IsIdle || slot < 0 || slot >= Slots.Count || slot == Selected)
            {
                return;
            }

            Selected = slot;
            Message = string.Empty;
            Revision++;
        }

        /// <summary>«Войти» выбранным персонажем.</summary>
        public void Enter()
        {
            if (CanEnter)
            {
                _flow!.Characters.Select(Selected);
            }
        }

        public void AskDelete()
        {
            if (CanDelete)
            {
                IsConfirmingDelete = true;
                Revision++;
            }
        }

        public void CancelDelete()
        {
            if (IsConfirmingDelete)
            {
                IsConfirmingDelete = false;
                Revision++;
            }
        }

        /// <summary>Удаление подтверждено: одна отправка, дальше ждём ответа сервера.</summary>
        public void ConfirmDelete()
        {
            if (!IsConfirmingDelete || _flow == null)
            {
                return;
            }

            IsConfirmingDelete = false;
            _deletingName = Slots[Selected].Name;
            _deleteRejected = false;
            IsDeleting = true;
            _flow.Characters.Delete(Selected);
            Revision++;
        }

        // Сервер на удаление всегда присылает новый список; при отказе перед ним — причину (AccountHandler.DeleteRequest).
        private void OnSlotsChanged()
        {
            if (IsDeleting && !_deleteRejected)
            {
                Message = "Персонаж «" + _deletingName + "» удалён.";
            }

            IsDeleting = false;
            Selected = -1;
            Revision++;
        }

        private void OnDeleteRejected(CharacterDeleteResult result)
        {
            _deleteRejected = true;
            Message = "Сервер не удалил персонажа «" + _deletingName + "»: " + LoginErrorText.DeleteRejected(result) + ".";
            Revision++;
        }

        private bool IsOccupied(int slot) => slot >= 0 && slot < Slots.Count && !Slots[slot].IsEmpty;

        private int FreeSlot()
        {
            var slots = Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
