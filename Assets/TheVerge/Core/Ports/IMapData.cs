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
    }
}
