using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.Items
{
    /// <summary>
    /// Открытые контейнеры и их содержимое (PLAN Стадия 2, п. 9а). Сервер открывает контейнер парой 0x24 и 0x3C
    /// (Container.DisplayTo): 0x3C — полный видимый список, поэтому чего в нём нет, то из контейнера убрано. Пустой 0x3C
    /// не называет контейнер — он относится к открытому только что (0x24).
    /// </summary>
    public sealed class ContainerContents
    {
        /// <summary>Номер окна книги заклинаний: сервер открывает её как контейнер с окном −1 (SendDisplaySpellbook).</summary>
        public const ushort SpellbookGump = 0xFFFF;

        private readonly EntityStore _store;
        private readonly List<Serial> _stale = new List<Serial>();
        private Serial _awaitingContent;
        private Serial _current;

        public ContainerContents(EntityStore store)
        {
            _store = store;
        }

        /// <summary>Сервер открыл контейнер: серийник и номер окна (gump).</summary>
        public event Action<Serial, ushort>? ContainerOpened;

        /// <summary>Пришёл полный список содержимого контейнера (0x3C) — уже разложен по миру.</summary>
        public event Action<Serial>? ContentReceived;

        public Serial OpenedContainer { get; private set; }

        public ushort OpenedGump { get; private set; }

        /// <summary>0x24: контейнер открыт; следующий 0x3C — его содержимое.</summary>
        public void OnOpen(Serial container, ushort gump)
        {
            OpenedContainer = container;
            OpenedGump = gump;
            _awaitingContent = container;
            ContainerOpened?.Invoke(container, gump);
        }

        /// <summary>Начало полного списка содержимого контейнера; <see cref="Serial.Zero"/> — список пуст (тот, что открыт).</summary>
        public void BeginContent(Serial container)
        {
            _current = container.IsValid ? container : _awaitingContent;
            _awaitingContent = Serial.Zero;
            var item = _store.FindItem(_current);
            if (item == null)
            {
                return;
            }

            foreach (var child in item.Contents)
            {
                child.PendingRemoval = true;
            }
        }

        /// <summary>Предмет из списка: лежит в контейнере (неизвестный контейнер — заглушка, EntityStore).</summary>
        public WorldResult AddContent(Serial serial, in ItemInfo info, Serial container, byte gridIndex) =>
            _store.PlaceInContainer(serial, info, container, gridIndex);

        /// <summary>Конец списка: что в контейнере было, а в списке не пришло, удаляется.</summary>
        public void EndContent()
        {
            var item = _store.FindItem(_current);
            _current = Serial.Zero;
            if (item == null)
            {
                return;
            }

            RemoveStale(item);
            ContentReceived?.Invoke(item.Serial);
        }

        private void RemoveStale(Item item)
        {
            foreach (var child in item.Contents)
            {
                if (child.PendingRemoval)
                {
                    _stale.Add(child.Serial);
                }
            }

            foreach (var serial in _stale)
            {
                _store.Remove(serial);
            }

            _stale.Clear();
        }
    }
}
