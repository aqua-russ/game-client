namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Время клиента в миллисекундах от любой точки отсчёта (AGENTS.md §3.1: никаких DateTime.Now и Time в Core).
    /// Unity даёт его из своего таймера, бот — из Stopwatch, тест двигает руками.
    /// </summary>
    public interface IClock
    {
        long NowMs { get; }
    }
}
