using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Tests.Network.Fuzz
{
    /// <summary>
    /// Целостность мира клиента: связи «контейнер ↔ содержимое» и «моб ↔ надетое» взаимны и указывают на объекты из реестра,
    /// циклов вложенности нет, поиск рядом совпадает с полным перебором (PLAN Стадия 2, п. 7, 8, 14).
    /// </summary>
    internal static class WorldInvariants
    {
        public static void Check(EntityStore store, Random random, string context, bool withSpatial = true)
        {
            foreach (var item in store.Items)
            {
                CheckItem(store, item, context);
            }

            foreach (var mobile in store.Mobiles)
            {
                Assert.That(store.FindMobile(mobile.Serial), Is.SameAs(mobile), context);
                for (var layer = 1; layer <= (int)Layer.Bank; layer++)
                {
                    var item = mobile.GetEquipped((Layer)layer);
                    Assert.That(item == null || (item.Owner == mobile && (int)item.Layer == layer && store.FindItem(item.Serial) == item), Is.True,
                        context + ": надетое на " + mobile.Serial);
                }
            }

            if (withSpatial)
            {
                CheckSpatial(store, random, context);
            }
        }

        private static void CheckItem(EntityStore store, Item item, string context)
        {
            var where = context + ": " + item;
            Assert.That(store.FindItem(item.Serial), Is.SameAs(item), where);
            Assert.That(item.Place == ItemPlace.Container, Is.EqualTo(item.Container != null), where + " — контейнер");
            Assert.That(item.Place == ItemPlace.Equipped, Is.EqualTo(item.Owner != null), where + " — надет");
            if (item.Container != null)
            {
                Assert.That(item.Container.Contents.Contains(item) && store.FindItem(item.Container.Serial) == item.Container, Is.True, where);
            }

            if (item.Owner != null)
            {
                Assert.That(item.Owner.GetEquipped(item.Layer), Is.SameAs(item), where);
            }

            foreach (var child in item.Contents)
            {
                Assert.That(child.Container, Is.SameAs(item), where + " — содержимое");
            }

            var steps = 0;
            for (var parent = item.Container; parent != null; parent = parent.Container)
            {
                Assert.That(++steps, Is.LessThanOrEqualTo(store.ItemCount), where + " — цикл вложенности");
            }
        }

        private static void CheckSpatial(EntityStore store, Random random, string context)
        {
            var mobiles = new List<Mobile>();
            var items = new List<Item>();
            for (var q = 0; q < 50; q++)
            {
                var x = random.Next(980, 1040);
                var y = random.Next(980, 1040);
                var r = random.Next(0, 25);
                store.Spatial.QueryMobiles(x, y, r, mobiles);
                store.Spatial.QueryItems(x, y, r, items);
                Assert.That(mobiles.Select(m => m.Serial.Value), Is.EquivalentTo(
                    store.Mobiles.Where(m => Near(m.Position, x, y, r)).Select(m => m.Serial.Value)), context + ": мобы рядом");
                Assert.That(items.Select(i => i.Serial.Value), Is.EquivalentTo(
                    store.Items.Where(i => i.Place == ItemPlace.World && Near(i.Position, x, y, r)).Select(i => i.Serial.Value)), context + ": предметы рядом");
            }
        }

        private static bool Near(Point3D p, int x, int y, int r) => Math.Abs(p.X - x) <= r && Math.Abs(p.Y - y) <= r;
    }
}
