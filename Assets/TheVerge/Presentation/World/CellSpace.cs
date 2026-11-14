using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Единственное место перевода клеток UO в координаты сцены (AGENTS.md §4.4, ADR 0015 п. 2): 1 клетка = 1 м,
    /// 1 единица Z = 0,1 м (как в ADR 0019). Клетка (x, y) занимает x…x+1 на восток и y…y+1 на юг; юг сцены — это −Z,
    /// поэтому север карты — вверх экрана, а восток — вправо.
    /// </summary>
    public static class CellSpace
    {
        public const float CellSize = 1f;
        public const float ZUnit = 0.1f;

        /// <summary>Угол клетки с наименьшими X и Z сцены, на высоте z.</summary>
        public static Vector3 CellMin(int x, int y, float z) => new Vector3(x * CellSize, z * ZUnit, -(y + 1) * CellSize);

        /// <summary>Середина клетки на высоте z.</summary>
        public static Vector3 CellCenter(int x, int y, float z) =>
            new Vector3((x + 0.5f) * CellSize, z * ZUnit, -(y + 0.5f) * CellSize);

        /// <summary>Высота в метрах по единицам Z сервера.</summary>
        public static float Height(int zUnits) => zUnits * ZUnit;
    }
}
