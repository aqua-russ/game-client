using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

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
        private readonly SpatialIndex _spatial;
        private readonly Stack<Item> _removing = new Stack<Item>();
        private readonly List<Mobile> _farMobiles = new List<Mobile>();
        private readonly List<Item> _farItems = new List<Item>();

        public EntityRemoval(Dictionary<uint, Mobile> mobiles, Dictionary<uint, Item> items, SpatialIndex spatial)
        {
            _mobiles = mobiles;
            _items = items;
            _spatial = spatial;
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

            _spatial.RemoveMobile(mobile);
            _mobiles.Remove(mobile.Serial.Value);
            removed?.Invoke(mobile.Serial);
        }

        /// <summary>Снять предмет с прежнего места, в том числе с земли (из поиска рядом). Содержимое остаётся при нём.</summary>
        public void Detach(Item item)
        {
            if (item.Place == ItemPlace.World)
            {
                _spatial.RemoveItem(item);
            }

            ItemTree.Detach(item);
        }

        /// <summary>Молча забыть всё.</summary>
        public void RemoveAll()
        {
            _mobiles.Clear();
            _items.Clear();
            _spatial.Clear();
        }

        /// <summary>Снять с моба надетое, которое полный список 0x78 не повторил.</summary>
        public void RemovePendingEquipment(Mobile mobile, Action<Serial>? removed)
        {
            for (var i = 1; i < Mobile.SlotCount; i++)
            {
                var item = mobile.GetEquipped((Layer)i);
                if (item != null && item.PendingUnequip)
                {
                    RemoveItem(item, removed);
                }
            }
        }

        public void RemoveItem(Item root, Action<Serial>? removed)
        {
            Detach(root);
            _removing.Push(root);
            while (_removing.Count > 0)
            {
                var item = _removing.Pop();
                ItemTree.PushContents(item, _removing);
                _items.Remove(item.Serial.Value);
                removed?.Invoke(item.Serial);
            }
        }

        /// <summary>
        /// Удалить мобов и предметы на земле дальше дальности обзора от своего персонажа: 18 клеток, multi — 22
        /// (Item.GetUpdateRange, BaseMulti.GetUpdateRange; квадрат, как Utility.InRange).
        /// </summary>
        public void RemoveOutOfRange(Mobile player, Action<Serial>? removed)
        {
            var center = player.Position;
            foreach (var mobile in _mobiles.Values)
            {
                if (mobile != player && !InRange(center, mobile.Position, ServerLimits.UpdateRange))
                {
                    _farMobiles.Add(mobile);
                }
            }

            foreach (var item in _items.Values)
            {
                var range = item.IsMulti ? ServerLimits.MultiUpdateRange : ServerLimits.UpdateRange;
                if (item.Place == ItemPlace.World && !InRange(center, item.Position, range))
                {
                    _farItems.Add(item);
                }
            }

            foreach (var mobile in _farMobiles)
            {
                RemoveMobile(mobile, removed);
            }

            foreach (var item in _farItems)
            {
                RemoveItem(item, removed);
            }

            _farMobiles.Clear();
            _farItems.Clear();
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

        private static bool InRange(Point3D center, Point3D point, int range) =>
            System.Math.Abs(point.X - center.X) <= range && System.Math.Abs(point.Y - center.Y) <= range;
    }
}
