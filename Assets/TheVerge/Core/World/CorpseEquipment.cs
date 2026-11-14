using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Вещи трупов по слоям (PLAN Стадия 4, п. 9): что было надето на умершем — по 0x89 сервера (CorpsePackets.cs,
    /// SendCorpseEquip: только трупы людей, которых видно; сами вещи — в трупе как в контейнере, 0x3C). Хранится, пока
    /// труп есть в мире: удалён труп или мир очищен — забыто. Неизвестный труп — пропуск и счётчик.
    /// </summary>
    public sealed class CorpseEquipment
    {
        private readonly Dictionary<uint, CorpseItem[]> _byCorpse = new Dictionary<uint, CorpseItem[]>();
        private readonly EntityStore _store;

        public CorpseEquipment(EntityStore store)
        {
            _store = store;
            store.Removed += serial => _byCorpse.Remove(serial.Value);
            store.Cleared += _byCorpse.Clear;
        }

        /// <summary>Вещи трупа пришли или сменились.</summary>
        public event Action<Serial>? Changed;

        /// <summary>Для скольких трупов известны вещи.</summary>
        public int Count => _byCorpse.Count;

        /// <summary>Сколько списков пришло для трупа, которого нет в мире.</summary>
        public long UnknownCorpse { get; private set; }

        /// <summary>Пришёл ли от сервера список вещей этого трупа (пустой — тоже ответ).</summary>
        public bool Has(Serial corpse) => _byCorpse.ContainsKey(corpse.Value);

        public IReadOnlyList<CorpseItem> Get(Serial corpse) =>
            _byCorpse.TryGetValue(corpse.Value, out var items) ? items : Array.Empty<CorpseItem>();

        public void Set(Serial corpse, IReadOnlyList<CorpseItem> items)
        {
            if (_store.FindItem(corpse) == null)
            {
                UnknownCorpse++;
                return;
            }

            var copy = new CorpseItem[items.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = items[i];
            }

            _byCorpse[corpse.Value] = copy;
            Changed?.Invoke(corpse);
        }
    }
}
