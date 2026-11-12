using System.Diagnostics;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>Часы бота для GameClient: мс от запуска, не зависят от перевода системных часов.</summary>
    public sealed class StopwatchClock : IClock
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();

        public long NowMs => _watch.ElapsedMilliseconds;
    }
}
