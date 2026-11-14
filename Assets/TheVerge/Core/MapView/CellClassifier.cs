using TheVerge.Core.Tiles;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Цвет клетки по флагам tiledata (PLAN Стадия 2, п. 13). Приоритет — от самого важного для хода: вода, стена,
    /// непроходимое, поверхность, остальное. Земля без особых флагов — проходимая поверхность: сервер ходит по ней,
    /// не требуя флага Surface (у земли в tiledata флаг Surface не ставится).
    /// </summary>
    public static class CellClassifier
    {
        public static CellClass Classify(TileFlags flags, bool isLand)
        {
            if ((flags & TileFlags.Wet) != 0)
            {
                return CellClass.Water;
            }

            if ((flags & TileFlags.Wall) != 0)
            {
                return CellClass.Wall;
            }

            if ((flags & TileFlags.Impassable) != 0)
            {
                return CellClass.Blocked;
            }

            return isLand || (flags & TileFlags.Surface) != 0 ? CellClass.Surface : CellClass.Other;
        }
    }
}
