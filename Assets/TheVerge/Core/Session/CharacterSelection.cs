using System;
using System.Collections.Generic;
using TheVerge.Core.Ports;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Список персонажей на игровом сервере (PLAN Стадия 1, п. 8): флаги возможностей, слоты, стартовые города, выбор
    /// персонажа. Часть входа (<see cref="LoginFlow.Characters"/>); состояние — общее (<see cref="LoginProgress"/>).
    /// </summary>
    public sealed class CharacterSelection
    {
        private readonly LoginProgress _progress;
        private readonly IAccountSender _sender;

        public CharacterSelection(LoginProgress progress, IAccountSender sender)
        {
            _progress = progress;
            _sender = sender;
        }

        /// <summary>Флаги возможностей сервера (SupportedFeatures); приходят перед списком и ещё раз при входе в мир.</summary>
        public uint SupportedFeatures { get; private set; }

        public IReadOnlyList<CharacterSlot> Slots { get; private set; } = Array.Empty<CharacterSlot>();

        public IReadOnlyList<StartingCity> Cities { get; private set; } = Array.Empty<StartingCity>();

        /// <summary>Имя персонажа, которого выбрали или создали последним; пусто — ещё не выбирали.</summary>
        public string SelectedName { get; private set; } = string.Empty;

        /// <summary>Флаги списка персонажей сервера (CharacterListFlags).</summary>
        public int ListFlags { get; private set; }

        public void OnSupportedFeatures(uint flags) => SupportedFeatures = flags;

        /// <summary>Забыть список перед новым входом (<see cref="LoginFlow.Restart"/>); выбранное имя остаётся.</summary>
        internal void Clear()
        {
            Slots = Array.Empty<CharacterSlot>();
            Cities = Array.Empty<StartingCity>();
        }

        /// <summary>Список слотов (вместе с пустыми) и стартовых городов. Ждёт выбора (<see cref="Select"/>).</summary>
        public void OnCharacterList(IReadOnlyList<CharacterSlot> slots, IReadOnlyList<StartingCity> cities, int flags)
        {
            if (_progress.State != LoginState.AwaitingCharacterList)
            {
                return;
            }

            Slots = slots;
            Cities = cities;
            ListFlags = flags;
            _progress.MoveTo(LoginState.CharacterSelection);
        }

        /// <summary>
        /// Войти в мир персонажем из слота. Пустой или несуществующий слот сервер наказывает отключением (PlayCharacter),
        /// поэтому такой выбор не уходит серверу.
        /// </summary>
        public void Select(int slot)
        {
            _progress.Require(LoginState.CharacterSelection, "Выбор персонажа");
            RequireOccupied(slot);
            SelectedName = Slots[slot].Name;
            _sender.SendPlayCharacter(slot, Slots[slot].Name);
            _progress.MoveTo(LoginState.EnteringWorld);
        }

        /// <summary>Список слотов обновлён (после удаления персонажа).</summary>
        public event Action? SlotsChanged;

        /// <summary>Сервер отказал в удалении; новый список придёт следом.</summary>
        public event Action<CharacterDeleteResult>? DeleteRejected;

        /// <summary>
        /// Создать персонажа и войти им в мир. Неверный выбор (правила — <see cref="CharacterCreationRules"/>), город не из
        /// списка (сервер за него отключает) или нет свободного слота — отказ, серверу ничего не уходит.
        /// </summary>
        public void Create(NewCharacter character)
        {
            _progress.Require(LoginState.CharacterSelection, "Создание персонажа");
            var problem = CharacterCreationRules.Validate(character);
            if (problem.Length == 0 && (character.CityIndex < 0 || character.CityIndex >= Cities.Count))
            {
                problem = "города " + character.CityIndex + " нет в списке сервера";
            }

            if (problem.Length > 0)
            {
                throw new ArgumentException("Персонажа так создать нельзя: " + problem + ".", nameof(character));
            }

            var slot = FreeSlot();
            if (slot < 0)
            {
                throw new InvalidOperationException("Свободных слотов нет: сначала удалите персонажа.");
            }

            SelectedName = character.Name;
            _sender.SendCreateCharacter(character, slot);
            _progress.MoveTo(LoginState.EnteringWorld);
        }

        /// <summary>Удалить персонажа. Ответ — новый список (<see cref="SlotsChanged"/>), при отказе перед ним — причина.</summary>
        public void Delete(int slot)
        {
            _progress.Require(LoginState.CharacterSelection, "Удаление персонажа");
            RequireOccupied(slot);
            _sender.SendDeleteCharacter(slot);
        }

        public void OnCharacterListUpdate(IReadOnlyList<CharacterSlot> slots)
        {
            if (_progress.State != LoginState.CharacterSelection)
            {
                return;
            }

            Slots = slots;
            SlotsChanged?.Invoke();
        }

        public void OnDeleteRejected(CharacterDeleteResult result) => DeleteRejected?.Invoke(result);

        /// <summary>Слот персонажа с таким именем (без учёта регистра) или -1.</summary>
        public int FindSlot(string name)
        {
            for (var i = 0; i < Slots.Count; i++)
            {
                if (!Slots[i].IsEmpty && string.Equals(Slots[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RequireOccupied(int slot)
        {
            if (slot < 0 || slot >= Slots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Слотов " + Slots.Count + ".");
            }

            if (Slots[slot].IsEmpty)
            {
                throw new ArgumentException("Слот " + slot + " пуст.", nameof(slot));
            }
        }

        private int FreeSlot()
        {
            for (var i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
