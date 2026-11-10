using System;
using System.Collections.Generic;

namespace TheVerge.Core
{
    /// <summary>
    /// Список персонажей учётной записи (пакет 0xA9) для отображения на экране выбора.
    /// </summary>
    public readonly struct CharacterListDto
    {
        public IReadOnlyList<CharacterSlotDto> Slots { get; }
        public int TotalSlots => Slots?.Count ?? 0;
        public bool HasAnyCharacter
        {
            get
            {
                if (Slots == null) return false;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (!Slots[i].IsEmpty) return true;
                }
                return false;
            }
        }

        public CharacterListDto(IReadOnlyList<CharacterSlotDto> slots)
        {
            Slots = slots ?? Array.Empty<CharacterSlotDto>();
        }

        public int FirstAvailableSlotIndex
        {
            get
            {
                if (Slots == null) return 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (Slots[i].IsEmpty) return Slots[i].SlotIndex;
                }
                return Slots.Count;
            }
        }
    }
}
