using System.Collections.Generic;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// Предмет с графикой на земле рядом со своим персонажем (для бинтов — не дальше 2 клеток, Bandage.Range сервера) или прямо
    /// в своём рюкзаке (эфирного коня сервер разрешает только «при себе» — EtherealMount.Validate).
    /// </summary>
    internal static class NearbyItems
    {
        public const int Reach = 2;

        /// <summary>Сначала на земле рядом, потом в своём рюкзаке.</summary>
        public static Item? FindNearOrInBackpack(EntityStore entities, ushort graphic, List<Item> buffer) =>
            Find(entities, graphic, buffer) ?? FindInBackpack(entities, graphic);

        private static Item? FindInBackpack(EntityStore entities, ushort graphic)
        {
            var backpack = entities.Player?.GetEquipped(TheVerge.Core.Primitives.Layer.Backpack);
            if (backpack == null)
            {
                return null;
            }

            foreach (var item in entities.Items)
            {
                if (item.Graphic == graphic && item.Container == backpack)
                {
                    return item;
                }
            }

            return null;
        }

        public static Item? Find(EntityStore entities, ushort graphic, List<Item> buffer)
        {
            var player = entities.Player;
            if (player == null)
            {
                return null;
            }

            entities.Spatial.QueryItems(player.Position.X, player.Position.Y, Reach, buffer);
            foreach (var item in buffer)
            {
                if (item.Graphic == graphic)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
