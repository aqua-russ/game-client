using System.Collections.Generic;
using TheVerge.Core.World;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Мобы и предметы на земле как блоки для выбора кликом: на тех же клетках и той же высоты, что рисует Presentation
    /// (<see cref="SceneLimits.MobileHeight"/>, <see cref="SceneLimits.ItemHeight"/>). Дома и корабли не входят: их части
    /// уже есть среди блоков <see cref="SceneBuilder"/>. Сначала предметы, потом мобы.
    /// </summary>
    public static class SceneMarkers
    {
        /// <summary>Блоки всех мобов и предметов на земле в мире клиента; <paramref name="result"/> очищается.</summary>
        public static void Collect(EntityStore world, List<SceneBlock> result)
        {
            result.Clear();
            foreach (var item in world.Items)
            {
                if (item.Place == ItemPlace.World && !item.IsMulti)
                {
                    var z = item.Position.Z;
                    result.Add(new SceneBlock(SceneKind.Item, item.Position.X, item.Position.Y, z, z + SceneLimits.ItemHeight, CellClass.Other));
                }
            }

            foreach (var mobile in world.Mobiles)
            {
                var z = mobile.Position.Z;
                result.Add(new SceneBlock(SceneKind.Mobile, mobile.Position.X, mobile.Position.Y, z, z + SceneLimits.MobileHeight, CellClass.Other));
            }
        }
    }
}
