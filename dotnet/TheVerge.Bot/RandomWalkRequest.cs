using System.Globalization;

namespace TheVerge.Bot
{
    /// <summary>
    /// randomwalk:&lt;шагов&gt;,&lt;номер&gt;,&lt;x0&gt;,&lt;y0&gt;,&lt;x1&gt;,&lt;y1&gt;,run|walk|mix — сколько шагов, номер для
    /// случайного выбора (один номер — одна прогулка), квадрат, из которого не выходить, и темп.
    /// </summary>
    public readonly struct RandomWalkRequest
    {
        private RandomWalkRequest(int steps, int seed, short x0, short y0, short x1, short y1, RandomWalkPace pace)
        {
            Steps = steps;
            Seed = seed;
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            Pace = pace;
        }

        public int Steps { get; }

        public int Seed { get; }

        public short X0 { get; }

        public short Y0 { get; }

        public short X1 { get; }

        public short Y1 { get; }

        public RandomWalkPace Pace { get; }

        public bool Contains(int x, int y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;

        public static bool TryParse(string text, out RandomWalkRequest request)
        {
            request = default;
            var parts = text.Split(',');
            if (parts.Length != 7 || !TryParsePace(parts[6], out var pace) ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int steps) ||
                steps < 1 || steps > WalkRequest.MaxSteps ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int seed) ||
                !BotAction.TryParseCell(parts[2] + "," + parts[3], out short x0, out short y0) ||
                !BotAction.TryParseCell(parts[4] + "," + parts[5], out short x1, out short y1) || x1 <= x0 || y1 <= y0)
            {
                return false;
            }

            request = new RandomWalkRequest(steps, seed, x0, y0, x1, y1, pace);
            return true;
        }

        private static bool TryParsePace(string text, out RandomWalkPace pace)
        {
            pace = text == "run" ? RandomWalkPace.Run : text == "walk" ? RandomWalkPace.Walk : RandomWalkPace.Mix;
            return text == "run" || text == "walk" || text == "mix";
        }
    }
}
