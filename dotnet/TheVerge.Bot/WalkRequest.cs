using System.Globalization;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// walk:&lt;направление&gt;,&lt;шагов&gt;,run|walk — куда, сколько шагов и бегом или шагом. Направления —
    /// <see cref="CompassDirection"/>.
    /// </summary>
    public readonly struct WalkRequest
    {
        /// <summary>Больше шагов за одно действие не нужно (самый длинный прогон walk.sh — десятки шагов).</summary>
        public const int MaxSteps = 10000;

        private WalkRequest(Direction direction, int steps, bool run)
        {
            Direction = direction;
            Steps = steps;
            Run = run;
        }

        public Direction Direction { get; }

        public int Steps { get; }

        public bool Run { get; }

        public MoveKeys HeldKeys => CompassDirection.KeysFor(Direction);

        public string Name => CompassDirection.NameOf(Direction);

        public static bool TryParse(string text, out WalkRequest request)
        {
            request = default;
            var parts = text.Split(',');
            if (parts.Length != 3 || !TryParseRun(parts[2], out bool run) || !CompassDirection.TryParse(parts[0], out var direction) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int steps) || steps < 1 || steps > MaxSteps)
            {
                return false;
            }

            request = new WalkRequest(direction, steps, run);
            return true;
        }

        /// <summary>«run» — бегом, «walk» — шагом.</summary>
        public static bool TryParseRun(string text, out bool run)
        {
            run = text == "run";
            return run || text == "walk";
        }
    }
}
