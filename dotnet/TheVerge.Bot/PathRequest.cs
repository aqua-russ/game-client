using System.Globalization;
using TheVerge.Core.Movement.Tooling;

namespace TheVerge.Bot
{
    /// <summary>path:x,y[,z],run|walk — цель для поиска пути A*, необязательная высота Z и бегом/шагом.</summary>
    public readonly struct PathRequest
    {
        private PathRequest(short x, short y, sbyte? z, bool run)
        {
            X = x;
            Y = y;
            Z = z;
            Run = run;
        }

        public short X { get; }

        public short Y { get; }

        public sbyte? Z { get; }

        public bool Run { get; }

        public PathGoal ToGoal() => new PathGoal(X, Y, Z);

        public static bool TryParse(string text, out PathRequest request)
        {
            request = default;
            var parts = text.Split(',');
            if ((parts.Length != 3 && parts.Length != 4) ||
                !WalkRequest.TryParseRun(parts[parts.Length - 1], out bool run) ||
                !BotAction.TryParseCell(parts[0] + "," + parts[1], out short x, out short y))
            {
                return false;
            }

            sbyte? z = null;
            if (parts.Length == 4)
            {
                if (!sbyte.TryParse(parts[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedZ))
                {
                    return false;
                }

                z = parsedZ;
            }

            request = new PathRequest(x, y, z, run);
            return true;
        }
    }
}
