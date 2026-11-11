using TheVerge.Core.Ports;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>Часы, которые двигает тест: обработка «стоит» столько, сколько он скажет.</summary>
    internal sealed class FakeClock : IClock
    {
        public long NowMs { get; set; }
    }
}
