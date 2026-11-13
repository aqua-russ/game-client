using System.Collections.Generic;

namespace TheVerge.Bot
{
    /// <summary>Итог сверки данных карты с дампом сервера: что совпало, первые расхождения и сколько их всего.</summary>
    public sealed class MapDumpReport
    {
        /// <summary>Сколько расхождений перечислять: дальше — только счёт (испорченный файл дал бы их миллионы).</summary>
        public const int MaxListed = 20;

        public List<string> Summary { get; } = new List<string>();
        public List<string> Differences { get; } = new List<string>();
        public int DifferenceCount { get; private set; }

        public void Differ(string what)
        {
            if (DifferenceCount++ < MaxListed)
            {
                Differences.Add(what);
            }
        }
    }
}
