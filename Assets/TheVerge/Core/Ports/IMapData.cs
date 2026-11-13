using System.Collections.Generic;
using TheVerge.Core.Tiles;

namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Карта: что лежит на клетке (PLAN Стадия 2, п. 3–4). Реализация — Data/Map/MapData; Core не знает, MUL это или
    /// UOP. Вызывается только из главного потока.
    /// </summary>
    public interface IMapData
    {
        int Width { get; }
        int Height { get; }

        /// <summary>Земля клетки; false — клетка вне карты (tile — пустая).</summary>
        bool TryGetLand(int x, int y, out LandTile tile);

        /// <summary>
        /// Статика клетки в порядке файла (как у сервера) — в <paramref name="result"/> (он очищается); false — клетка
        /// вне карты. Новой памяти не требует, если в списке хватает места.
        /// </summary>
        bool TryGetStatics(int x, int y, List<StaticTile> result);
    }
}
