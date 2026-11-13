using System.Globalization;

namespace TheVerge.Bot
{
    /// <summary>
    /// mapdump:&lt;клеток&gt;,&lt;seed&gt;,&lt;папка&gt;[,all-statics] — что попросить у сервера командой [DumpMapData
    /// (Custom/TheVerge/Parity/DumpMapDataCommand.cs: клеток 1..1 000 000) и где ждать файл.
    /// </summary>
    public readonly struct MapDumpRequest
    {
        public const int MaxCells = 1000000;
        private const string AllStaticsOption = "all-statics";

        private MapDumpRequest(int cells, int seed, string directory, bool allStatics)
        {
            Cells = cells;
            Seed = seed;
            Directory = directory;
            AllStatics = allStatics;
        }

        public int Cells { get; }
        public int Seed { get; }
        public string Directory { get; }

        /// <summary>Ещё все клетки со статикой — только для маленьких карт (сервер прочтёт статику всей карты).</summary>
        public bool AllStatics { get; }

        public string Command => "[DumpMapData " + Cells.ToString(CultureInfo.InvariantCulture) + " " +
                                 Seed.ToString(CultureInfo.InvariantCulture) + (AllStatics ? " " + AllStaticsOption : string.Empty);

        public static bool TryParse(string text, out MapDumpRequest request)
        {
            request = default;
            var first = text.IndexOf(',');
            var second = first < 0 ? -1 : text.IndexOf(',', first + 1);
            if (second < 0 ||
                !int.TryParse(text.Substring(0, first), NumberStyles.None, CultureInfo.InvariantCulture, out var cells) ||
                !int.TryParse(text.Substring(first + 1, second - first - 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed) ||
                cells < 1 || cells > MaxCells)
            {
                return false;
            }

            var directory = text.Substring(second + 1);
            var allStatics = directory.EndsWith("," + AllStaticsOption, System.StringComparison.Ordinal);
            if (allStatics)
            {
                directory = directory.Substring(0, directory.Length - AllStaticsOption.Length - 1);
            }

            request = new MapDumpRequest(cells, seed, directory, allStatics);
            return directory.Length > 0;
        }
    }
}
