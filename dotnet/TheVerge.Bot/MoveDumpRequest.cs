using System.Globalization;

namespace TheVerge.Bot
{
    /// <summary>
    /// movedump:&lt;проверок&gt;,&lt;seed&gt;,&lt;папка&gt; — что попросить у сервера командой [DumpMove
    /// (Custom/TheVerge/Parity/DumpMoveCommand.cs: проверок 1..200 000) и где ждать файл.
    /// </summary>
    public readonly struct MoveDumpRequest
    {
        public const int MaxCount = 200000;

        private MoveDumpRequest(int count, int seed, string directory)
        {
            Count = count;
            Seed = seed;
            Directory = directory;
        }

        public int Count { get; }
        public int Seed { get; }
        public string Directory { get; }

        public string Command => "[DumpMove " + Count.ToString(CultureInfo.InvariantCulture) + " " +
                                 Seed.ToString(CultureInfo.InvariantCulture);

        public static bool TryParse(string text, out MoveDumpRequest request)
        {
            request = default;
            var first = text.IndexOf(',');
            var second = first < 0 ? -1 : text.IndexOf(',', first + 1);
            if (second < 0 ||
                !int.TryParse(text.Substring(0, first), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
                !int.TryParse(text.Substring(first + 1, second - first - 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed) ||
                count < 1 || count > MaxCount)
            {
                return false;
            }

            var directory = text.Substring(second + 1);
            request = new MoveDumpRequest(count, seed, directory);
            return directory.Length > 0;
        }
    }
}
