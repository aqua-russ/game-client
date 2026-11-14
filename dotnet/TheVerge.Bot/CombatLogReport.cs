using System.Globalization;
using TheVerge.Core.Combat;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// Журнал боя в лог бота (PLAN Стадия 4, п. 8): каждое событие строкой «Журнал боя: +с от первого события, что, кто,
    /// по кому» — по ним combat.sh сверяет порядок с логом пакетов; в конце — итог по видам.
    /// </summary>
    internal static class CombatLogReport
    {
        public const string Prefix = "Журнал боя: ";

        public static void Write(ILog log, CombatLog journal, Serial player, Serial opponent)
        {
            var counts = new int[6];
            var start = journal.Count > 0 ? journal[0].TimeMs : 0;
            for (var i = 0; i < journal.Count; i++)
            {
                var e = journal[i];
                log.Info(Prefix + "+" + ((e.TimeMs - start) / 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + " с " + Describe(e) + ".");
                Count(e, player, opponent, counts);
            }

            log.Info(Prefix + "событий " + journal.Count + " (всего пришло " + journal.Total + "): замахов бота по противнику " +
                     counts[0] + ", анимаций бота " + counts[1] + ", противника " + counts[2] + ", урона " + counts[3] +
                     ", эффектов " + counts[4] + ", звуков " + counts[5] + ".");
        }

        private static string Describe(in CombatEvent e)
        {
            switch (e.Kind)
            {
                case CombatEventKind.Swing:
                    return "замах " + e.Source + " → " + e.Target;
                case CombatEventKind.Animation:
                    return "анимация " + e.Source + " действие " + e.Value;
                case CombatEventKind.Damage:
                    return "урон " + e.Target + ": " + e.Value;
                case CombatEventKind.Effect:
                    return "эффект " + (EffectKind)e.Detail + " 0x" + e.Value.ToString("X4") + " " + e.Source + " → " + e.Target;
                case CombatEventKind.Death:
                    return "смерть " + e.Source + " труп " + e.Target;
                default:
                    return "звук 0x" + e.Value.ToString("X4") + " в " + e.Position;
            }
        }

        private static void Count(in CombatEvent e, Serial player, Serial opponent, int[] counts)
        {
            switch (e.Kind)
            {
                case CombatEventKind.Swing when e.Source == player && e.Target == opponent:
                    counts[0]++;
                    break;
                case CombatEventKind.Animation when e.Source == player:
                    counts[1]++;
                    break;
                case CombatEventKind.Animation when e.Source == opponent:
                    counts[2]++;
                    break;
                case CombatEventKind.Damage:
                    counts[3]++;
                    break;
                case CombatEventKind.Effect:
                    counts[4]++;
                    break;
                case CombatEventKind.Sound:
                    counts[5]++;
                    break;
            }
        }
    }
}
