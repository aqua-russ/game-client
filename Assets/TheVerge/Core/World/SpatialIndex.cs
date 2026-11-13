using System.Collections.Generic;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Поиск объектов рядом с клеткой (PLAN Стадия 2, п. 8): мобы и предметы на земле. Дальность — квадрат, как у сервера
    /// (Utility.InRange: |dx| ≤ r и |dy| ≤ r). Карта у клиента одна (при смене мир очищается), поэтому её в запросе нет.
    /// Ведёт его <see cref="EntityStore"/>: каждый объект на земле лежит здесь в клетке, где он сейчас.
    /// </summary>
    public sealed class SpatialIndex
    {
        private readonly BlockGrid<Mobile> _mobiles = new BlockGrid<Mobile>();
        private readonly BlockGrid<Item> _items = new BlockGrid<Item>();

        /// <summary>Мобы в радиусе — в <paramref name="results"/> (список очищается; новой памяти не берёт, если хватает места).</summary>
        public void QueryMobiles(int x, int y, int radius, List<Mobile> results) => _mobiles.Query(x, y, radius, results);

        /// <summary>Предметы на земле в радиусе — в <paramref name="results"/> (список очищается).</summary>
        public void QueryItems(int x, int y, int radius, List<Item> results) => _items.Query(x, y, radius, results);

        internal void AddMobile(Mobile mobile) => _mobiles.Add(mobile, mobile.Position.X, mobile.Position.Y);

        internal void MoveMobile(Mobile mobile, Point3D from)
        {
            if (from.X == mobile.Position.X && from.Y == mobile.Position.Y)
            {
                return;
            }

            _mobiles.Remove(mobile, from.X, from.Y);
            AddMobile(mobile);
        }

        internal void RemoveMobile(Mobile mobile) => _mobiles.Remove(mobile, mobile.Position.X, mobile.Position.Y);

        internal void AddItem(Item item) => _items.Add(item, item.Position.X, item.Position.Y);

        internal void RemoveItem(Item item) => _items.Remove(item, item.Position.X, item.Position.Y);

        internal void Clear()
        {
            _mobiles.Clear();
            _items.Clear();
        }
    }
}
