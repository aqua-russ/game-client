using TheVerge.Core.Combat;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>Смерти из журнала боя (0xAF): чей труп у моба.</summary>
    internal static class DeathRecords
    {
        /// <summary>Труп моба по последней анимации его смерти; 0 — смерти в журнале нет.</summary>
        public static Serial CorpseOf(CombatLog journal, Serial mobile)
        {
            for (var i = journal.Count - 1; i >= 0; i--)
            {
                var e = journal[i];
                if (e.Kind == CombatEventKind.Death && e.Source == mobile)
                {
                    return e.Target;
                }
            }

            return Serial.Zero;
        }
    }
}
