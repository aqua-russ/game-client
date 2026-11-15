using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// Стороны света в действиях бота: n, ne, e, se, s, sw, w, nw — направления UO North … Up; клавиши для них — по раскладке
    /// «север сверху» (PLAN Стадия 3, п. 1): W = North, D = East, пары — диагонали.
    /// </summary>
    public static class CompassDirection
    {
        private static readonly string[] Names = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };

        private static readonly MoveKeys[] ScreenKeys =
        {
            MoveKeys.Up, MoveKeys.Up | MoveKeys.Right, MoveKeys.Right, MoveKeys.Right | MoveKeys.Down,
            MoveKeys.Down, MoveKeys.Down | MoveKeys.Left, MoveKeys.Left, MoveKeys.Left | MoveKeys.Up
        };

        public static bool TryParse(string text, out Direction direction)
        {
            int index = System.Array.IndexOf(Names, text);
            direction = index < 0 ? Direction.North : (Direction)index;
            return index >= 0;
        }

        public static string NameOf(Direction direction) => Names[(int)(direction & Direction.FacingMask)];

        public static MoveKeys KeysFor(Direction direction) => ScreenKeys[(int)(direction & Direction.FacingMask)];

        /// <summary>
        /// Шаг из <paramref name="from"/> к клетке (x, y): по диагонали, пока не совпадёт одна из осей, потом по прямой.
        /// false — уже там.
        /// </summary>
        public static bool TryStepToward(Point3D from, short x, short y, out Direction direction)
        {
            int dx = System.Math.Sign(x - from.X);
            int dy = System.Math.Sign(y - from.Y);
            direction = (dx, dy) switch
            {
                (0, -1) => Direction.North,
                (1, -1) => Direction.Right,
                (1, 0) => Direction.East,
                (1, 1) => Direction.Down,
                (0, 1) => Direction.South,
                (-1, 1) => Direction.Left,
                (-1, 0) => Direction.West,
                _ => Direction.Up,
            };
            return dx != 0 || dy != 0;
        }
    }
}
