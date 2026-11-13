using System.Diagnostics;
using TheVerge.Core.Ports;

namespace TheVerge.Tests.Bot
{
    /// <summary>Часы для тестов бота.</summary>
    internal static class BotTestClocks
    {
        /// <summary>Настоящее время.</summary>
        public sealed class Real : IClock
        {
            private readonly Stopwatch _watch = Stopwatch.StartNew();

            public long NowMs => _watch.ElapsedMilliseconds;
        }

        /// <summary>Часы, которые на каждый вопрос уходят вперёд: таймауты в десятки секунд проходят за миллисекунды.</summary>
        public sealed class Stepping : IClock
        {
            private readonly long _step;
            private long _now;

            public Stepping(long step)
            {
                _step = step;
            }

            public long NowMs => _now += _step;
        }
    }
}
