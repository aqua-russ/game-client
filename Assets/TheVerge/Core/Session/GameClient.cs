using TheVerge.Core.Ports;

namespace TheVerge.Core.Session
{
    /// <summary>
    /// Ядро клиента (AGENTS.md §2.3; ADR 0013 п. 3, 7). Один <see cref="Tick"/> на кадр Unity или шаг цикла бота, всегда
    /// в одном потоке: разбирает пришедшее по порядку, пока не вышел бюджет тика (ClientLimits.TickBudgetMs), но не
    /// меньше одного события за тик, а в конце отправляет накопленное за тик одним вызовом. Номеров пакетов ядро
    /// не знает: разбор — за портом <see cref="INetworkSession"/>.
    /// </summary>
    public sealed class GameClient
    {
        private readonly INetworkSession _network;
        private readonly IClock _clock;

        public GameClient(INetworkSession network, IClock clock)
        {
            _network = network;
            _clock = clock;
        }

        /// <summary>
        /// Один тик. <paramref name="now"/> — начало кадра: бюджет считается от него по <see cref="IClock"/>, поэтому то,
        /// что кадр уже потратил до вызова, тоже идёт в счёт. Возвращает, сколько событий разобрано.
        /// </summary>
        public int Tick(long now)
        {
            var deadline = now + ClientLimits.TickBudgetMs;
            var processed = 0;
            while (_network.ProcessNext())
            {
                processed++;
                if (_clock.NowMs >= deadline)
                {
                    break;
                }
            }

            _network.Flush();
            return processed;
        }
    }
}
