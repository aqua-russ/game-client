using System.Diagnostics;
using TheVerge.Core.Ports;

namespace TheVerge.Bootstrap
{
    /// <summary>Часы ядра в Unity: мс от запуска, не зависят от перевода системных часов и от Time.timeScale.</summary>
    public sealed class UnityClock : IClock
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();

        public long NowMs => _watch.ElapsedMilliseconds;
    }
}
