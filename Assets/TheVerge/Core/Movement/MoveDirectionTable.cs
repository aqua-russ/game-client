using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Единственная таблица «зажатые клавиши → направление UO» (PLAN Стадия 3, п. 1; CONTROLS §4). Сначала клавиши дают
    /// направление на экране (противоположные гасят друг друга), потом раскладка камеры переводит его в направление UO.
    /// Новая камера — новая строка в <see cref="ScreenToUo"/>, остальной код не меняется.
    /// </summary>
    public static class MoveDirectionTable
    {
        private const int NoScreenDirection = -1;

        // Направления на экране по часовой стрелке: 0 вверх, 1 вверх-вправо, 2 вправо, 3 вниз-вправо, 4 вниз,
        // 5 вниз-влево, 6 влево, 7 вверх-влево. Индекс — набор MoveKeys (Up 1, Right 2, Down 4, Left 8).
        private static readonly sbyte[] KeysToScreen =
        {
            NoScreenDirection, // —
            0,                 // W
            2,                 // D
            1,                 // W+D
            4,                 // S
            NoScreenDirection, // W+S
            3,                 // D+S
            2,                 // W+D+S
            6,                 // A
            7,                 // W+A
            NoScreenDirection, // D+A
            0,                 // W+D+A
            5,                 // S+A
            6,                 // W+S+A
            4,                 // D+S+A
            NoScreenDirection  // W+D+S+A
        };

        // Направление UO для каждого направления экрана, по раскладке камеры (CameraLayout).
        private static readonly Direction[][] ScreenToUo =
        {
            // Север сверху: W = North, D = East, S = South, A = West.
            new[] { Direction.North, Direction.Right, Direction.East, Direction.Down, Direction.South, Direction.Left, Direction.West, Direction.Up },
            // Изометрия: север — вверх-вправо, поэтому W = Up, а W+D = North.
            new[] { Direction.Up, Direction.North, Direction.Right, Direction.East, Direction.Down, Direction.South, Direction.Left, Direction.West }
        };

        /// <summary>Направление UO для зажатых клавиш; false — клавиш нет или они гасят друг друга (стоим).</summary>
        public static bool TryGetDirection(MoveKeys keys, CameraLayout layout, out Direction direction)
        {
            int screen = KeysToScreen[(int)keys & 0xF];
            if (screen == NoScreenDirection)
            {
                direction = Direction.North;
                return false;
            }

            direction = ScreenToUo[(int)layout][screen];
            return true;
        }

        /// <summary>Пара соседних клавиш — диагональ на экране: её не нужно ждать второй клавишей (PLAN 3.1, ~50 мс).</summary>
        public static bool IsScreenDiagonal(MoveKeys keys)
        {
            int screen = KeysToScreen[(int)keys & 0xF];
            return screen != NoScreenDirection && (screen & 1) == 1;
        }
    }
}
