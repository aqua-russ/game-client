using System.Collections.Generic;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>Связи предмета с местом: снять с места, проверить вложенность, обойти содержимое.</summary>
    internal static class ItemTree
    {
        /// <summary>Снять предмет с прежнего места (контейнер, слот моба, земля). Содержимое остаётся при нём.</summary>
        public static void Detach(Item item)
        {
            if (item.Container != null)
            {
                item.Container.RemoveContent(item);
                item.Container = null;
            }

            if (item.Owner != null)
            {
                item.Owner.SetSlot(item.Layer, null);
                item.Owner = null;
            }

            item.Layer = Layer.Invalid;
            item.Place = ItemPlace.Unplaced;
        }

        /// <summary>
        /// Лежит ли <paramref name="candidate"/> внутри <paramref name="container"/> (или это он сам). Пустой предмет не может
        /// содержать ничего, кроме себя, — так новые предметы проверяются сразу, без обхода цепочки родителей.
        /// </summary>
        public static bool Contains(Item container, Item candidate)
        {
            if (container == candidate)
            {
                return true;
            }

            if (container.Contents.Count == 0)
            {
                return false;
            }

            for (var parent = candidate.Container; parent != null; parent = parent.Container)
            {
                if (parent == container)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Положить в стек всё, что лежит в предмете, и забыть связи с ним.</summary>
        public static void PushContents(Item item, Stack<Item> stack)
        {
            var contents = item.ContentsList;
            if (contents == null)
            {
                return;
            }

            foreach (var child in contents)
            {
                child.Container = null;
                child.Place = ItemPlace.Unplaced;
                stack.Push(child);
            }

            contents.Clear();
        }
    }
}
