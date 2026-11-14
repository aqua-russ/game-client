using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>Часы записи посредника: время — из строки записи, а не настоящее.</summary>
    internal sealed class CaptureClock : IClock
    {
        public long NowMs { get; set; }
    }
}
