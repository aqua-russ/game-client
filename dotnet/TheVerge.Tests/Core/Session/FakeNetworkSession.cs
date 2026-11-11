using System.Collections.Generic;
using TheVerge.Core.Ports;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Сеть для тестов ядра: очередь «пакетов» (номеров по порядку), у каждого — сколько мс занимает обработка.
    /// Записывает, что и в каком порядке делало ядро: «обработал N» и «отправил».
    /// </summary>
    internal sealed class FakeNetworkSession : INetworkSession
    {
        private readonly Queue<(int Number, long CostMs)> _queue = new Queue<(int, long)>();
        private readonly FakeClock _clock;

        public FakeNetworkSession(FakeClock clock)
        {
            _clock = clock;
        }

        public List<string> Calls { get; } = new List<string>();

        public bool IsConnected => true;

        public void Push(int number, long costMs) => _queue.Enqueue((number, costMs));

        public bool ProcessNext()
        {
            if (_queue.Count == 0)
            {
                return false;
            }

            var (number, cost) = _queue.Dequeue();
            _clock.NowMs += cost;
            Calls.Add("обработал " + number);
            return true;
        }

        public void Flush() => Calls.Add("отправил");
    }
}
