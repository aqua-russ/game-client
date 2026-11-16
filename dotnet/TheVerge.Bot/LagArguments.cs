using System.Globalization;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>
    /// Разбор параметров задержки сети для бота (PLAN Стадия 3, п. 12): <c>--lag</c>, <c>--lag-in</c>, <c>--lag-out</c>.
    /// Пределы общие с Unity — <see cref="ClientLimits"/> (0..1000 мс, разброс ±0..500 мс).
    /// </summary>
    internal static class LagArguments
    {
        public static LagSimulation CreateEmpty() => new LagSimulation
        {
            IncomingDelayMs = 0,
            IncomingJitterMs = 0,
            OutgoingDelayMs = 0,
            OutgoingJitterMs = 0,
        };

        public static string Apply(string flag, string value, LagSimulation lag, bool incoming, bool outgoing)
        {
            if (!TryParse(value, out int delayMs, out int jitterMs))
            {
                return flag + ": нужно «задержка[,разброс]», например 50,15 (задержка 0.." +
                       ClientLimits.LagSimulationMaxDelayMs + " мс, разброс 0.." + ClientLimits.LagSimulationMaxJitterMs + " мс).";
            }

            if (incoming)
            {
                lag.IncomingDelayMs = delayMs;
                lag.IncomingJitterMs = jitterMs;
            }

            if (outgoing)
            {
                lag.OutgoingDelayMs = delayMs;
                lag.OutgoingJitterMs = jitterMs;
            }

            lag.Enabled = lag.IncomingDelayMs > 0 || lag.IncomingJitterMs > 0 ||
                          lag.OutgoingDelayMs > 0 || lag.OutgoingJitterMs > 0;
            return string.Empty;
        }

        public static bool TryParse(string text, out int delayMs, out int jitterMs)
        {
            delayMs = 0;
            jitterMs = 0;
            var parts = text.Split(',');
            if (parts.Length > 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out delayMs) ||
                delayMs > ClientLimits.LagSimulationMaxDelayMs)
            {
                return false;
            }

            if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out jitterMs) ||
                                      jitterMs > ClientLimits.LagSimulationMaxJitterMs))
            {
                return false;
            }

            return true;
        }
    }
}