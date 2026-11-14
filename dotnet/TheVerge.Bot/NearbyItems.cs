using System.Collections.Generic;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>Предмет с графикой на земле рядом со своим персонажем (для бинтов — не дальше 2 клеток, Bandage.Range сервера).</summary>
    internal static class NearbyItems
    {
        public const int Reach = 2;

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
