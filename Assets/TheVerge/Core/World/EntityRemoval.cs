using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Удаление объектов из реестра <see cref="EntityStore"/> вместе со всем вложенным и надетым. Без рекурсии:
    /// вложенность контейнеров может быть сколь угодно глубокой. О каждом удалённом сообщает <c>removed</c> (null — молча).
    /// </summary>
    internal sealed class EntityRemoval
    {
        private readonly Dictionary<uint, Mobile> _mobiles;
        private readonly Dictionary<uint, Item> _items;
        private readonly Stack<Item> _removing = new Stack<Item>();

        public EntityRemoval(Dictionary<uint, Mobile> mobiles, Dictionary<uint, Item> items)
        {
            _mobiles = mobiles;
            _items = items;
        }

        public void RemoveMobile(Mobile mobile, Action<Serial>? removed)
        {
            for (var i = 1; i < Mobile.SlotCount; i++)
            {
                var item = mobile.GetEquipped((Layer)i);
                if (item != null)
                {
                    RemoveItem(item, removed);
                }
            }

            _mobiles.Remove(mobile.Serial.Value);
            removed?.Invoke(mobile.Serial);
        }

        public void RemoveItem(Item root, Action<Serial>? removed)
        {
            ItemTree.Detach(root);
            _removing.Push(root);
            while (_removing.Count > 0)
            {
                var item = _removing.Pop();
                ItemTree.PushContents(item, _removing);
                _items.Remove(item.Serial.Value);
                removed?.Invoke(item.Serial);
            }
        }

        /// <summary>Молча удалить всё, кроме своего персонажа, его надетого и содержимого.</summary>
        public void RemoveAllExcept(Mobile player)
        {
            var mobiles = new List<Mobile>(_mobiles.Values);
            foreach (var mobile in mobiles)
            {
                if (mobile != player)
                {
                    RemoveMobile(mobile, null);
                }
            }

            // Остались предметы своего персонажа и «корни»: на земле и заглушки-контейнеры без места.
            var roots = new List<Item>();
            foreach (var item in _items.Values)
            {
                if (item.Place == ItemPlace.World || (item.Place == ItemPlace.Unplaced && item.Container == null))
                {
                    roots.Add(item);
                }
            }

            foreach (var item in roots)
            {
                RemoveItem(item, null);
            }
        }
    }
}
