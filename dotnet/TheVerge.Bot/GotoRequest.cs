namespace TheVerge.Bot
{
    /// <summary>goto:x,y,run|walk — до какой клетки идти и бегом или шагом.</summary>
    public readonly struct GotoRequest
    {
        private GotoRequest(short x, short y, bool run)
        {
            X = x;
            Y = y;
            Run = run;
        }

        public short X { get; }

        public short Y { get; }

        public bool Run { get; }

        public static bool TryParse(string text, out GotoRequest request)
        {
            request = default;
            int comma = text.LastIndexOf(',');
            if (comma < 0 || !WalkRequest.TryParseRun(text.Substring(comma + 1), out bool run) ||
                !BotAction.TryParseCell(text.Substring(0, comma), out short x, out short y))
            {
                return false;
            }

            request = new GotoRequest(x, y, run);
            return true;
        }
    }
}
