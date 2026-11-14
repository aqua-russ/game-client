using System.Collections.Generic;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>Итог сверки с дампом сервера (карта, состояние мобов): что совпало, первые расхождения и сколько их всего.</summary>
    public sealed class ParityReport
    {
        /// <summary>Сколько расхождений перечислять: дальше — только счёт (испорченный файл дал бы их миллионы).</summary>
        public const int MaxListed = 20;

        public List<string> Summary { get; } = new List<string>();
        public List<string> Differences { get; } = new List<string>();
        public int DifferenceCount { get; private set; }

        /// <summary>
        /// Итог сверки в журнал бота (общий для mapdump:, statusdump:, movedump:): сводка, затем «совпадает» или список
        /// расхождений. Не совпало — <paramref name="failure"/>: текст провала шага с первым расхождением и путём дампа.
        /// </summary>
        public bool WriteTo(ILog log, string matchText, string mismatchText, string path, out string failure)
        {
            failure = string.Empty;
            foreach (var line in Summary)
            {
                log.Info(line);
            }

            if (DifferenceCount == 0)
            {
                log.Info(matchText);
                return true;
            }

            foreach (var line in Differences)
            {
                log.Warning("Расхождение: " + line);
            }

            if (DifferenceCount > Differences.Count)
            {
                log.Warning("…и ещё расхождений: " + (DifferenceCount - Differences.Count) + ".");
            }

            failure = mismatchText + ": " + DifferenceCount + " (первое — " + Differences[0] + "). Дамп: " + path + ".";
            return false;
        }

        public void Differ(string what)
        {
            if (DifferenceCount++ < MaxListed)
            {
                Differences.Add(what);
            }
        }
    }
}
