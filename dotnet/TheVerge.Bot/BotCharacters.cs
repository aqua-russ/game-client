using System.Collections.Generic;
using System.Text;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>Персонажи для бота: описание списка, первый занятый слот и персонаж для создания.</summary>
    internal static class BotCharacters
    {
        public static string Describe(IReadOnlyList<CharacterSlot> slots)
        {
            var text = new StringBuilder();
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    text.Append(text.Length > 0 ? ", " : string.Empty).Append(slot.Name);
                }
            }

            return text.Length > 0 ? text.ToString() : "нет";
        }

        public static int FirstOccupied(IReadOnlyList<CharacterSlot> slots)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Персонаж бота по правилам TARGET.md §4: статы 35/35/20 (сумма 90), Magery (25) и Wrestling (43) по 50 (сумма 100),
        /// первый город из списка сервера.
        /// </summary>
        public static NewCharacter NewCharacter(string name) => new NewCharacter
        {
            Name = name,
            Strength = 35,
            Dexterity = 35,
            Intelligence = 20,
            Skills = new[] { (25, 50), (43, 50), (0, 0), (1, 0) },
            CityIndex = 0,
        };
    }
}
