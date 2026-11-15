using System.Globalization;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// walk:&lt;направление&gt;,&lt;шагов&gt;,run|walk — куда, сколько шагов и бегом или шагом. Направления — стороны света UO:
    /// n, ne, e, se, s, sw, w, nw; клавиши для них — по раскладке «север сверху» (PLAN Стадия 3, п. 1).
    /// </summary>
    public readonly struct WalkRequest
    {
        /// <summary>Больше шагов за одно действие не нужно (самый длинный прогон walk.sh — десятки шагов).</summary>
        public const int MaxSteps = 10000;

        private static readonly string[] Names = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };

        // Клавиши экрана для направления UO при камере «север сверху»: W = North, D = East, пары — диагонали.
        private static readonly MoveKeys[] ScreenKeys =
        {
            MoveKeys.Up, MoveKeys.Up | MoveKeys.Right, MoveKeys.Right, MoveKeys.Right | MoveKeys.Down,
            MoveKeys.Down, MoveKeys.Down | MoveKeys.Left, MoveKeys.Left, MoveKeys.Left | MoveKeys.Up
        };

        private WalkRequest(Direction direction, int steps, bool run)
        {
            Direction = direction;
            Steps = steps;
            Run = run;
        }

        public Direction Direction { get; }

        public int Steps { get; }

        public bool Run { get; }

        public MoveKeys HeldKeys => ScreenKeys[(int)Direction];

        public string Name => Names[(int)Direction];

        public static bool TryParse(string text, out WalkRequest request)
        {
            request = default;
            var parts = text.Split(',');
            if (parts.Length != 3 || parts[2] != "run" && parts[2] != "walk")
            {
                return false;
            }

            int direction = System.Array.IndexOf(Names, parts[0]);
            if (direction < 0 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int steps) ||
                steps < 1 || steps > MaxSteps)
            {
                return false;
            }

            request = new WalkRequest((Direction)direction, steps, parts[2] == "run");
            return true;
        }
    }
}
