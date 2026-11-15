using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TheVerge.Core.Diagnostics;

namespace TheVerge.Network.Diagnostics
{
    /// <summary>
    /// Очередь задержки одного направления для симуляции пинга (PLAN Стадия 3, п. 9; ADR 0020 п. 9): элемент выходит не
    /// раньше своего срока (приход плюс задержка по <see cref="LagSettings"/>) и не раньше всех, кто пришёл до него. Пакеты
    /// в TCP не обгоняют друг друга, поэтому разброс задержки даёт пачки, а не перестановку, и протокол симуляции не
    /// замечает. Чистая логика без сокетов и Unity: её берёт соединение-обёртка <see cref="LaggedConnection"/>, а задача 3.12
    /// может взять для посредника бота. Новой памяти в установившемся режиме нет.
    /// </summary>
    public sealed class DelayLine<T>
    {
        private readonly Queue<Entry> _queue = new Queue<Entry>();
        private readonly Random _random;
        private long _lastReleaseMs = long.MinValue;

        public DelayLine(Random random)
        {
            _random = random;
        }

        /// <summary>Сколько элементов ждёт своего срока.</summary>
        public int Count => _queue.Count;

        /// <summary>Элемент пришёл в момент <paramref name="now"/>; его задержка — по <paramref name="lag"/>.</summary>
        public void Push(T item, long now, LagSettings lag)
        {
            long release = Math.Max(now + lag.SampleMs(_random), _lastReleaseMs);
            _lastReleaseMs = release;
            _queue.Enqueue(new Entry(item, release));
        }

        /// <summary>Следующий элемент, если его срок наступил к <paramref name="now"/>.</summary>
        public bool TryPop(long now, [MaybeNullWhen(false)] out T item)
        {
            if (_queue.Count == 0 || _queue.Peek().ReleaseMs > now)
            {
                item = default;
                return false;
            }

            item = _queue.Dequeue().Item;
            return true;
        }

        /// <summary>Следующий элемент без учёта срока (закрытие: вернуть всё, что держится).</summary>
        public bool TryPopAny([MaybeNullWhen(false)] out T item)
        {
            if (_queue.Count == 0)
            {
                item = default;
                return false;
            }

            item = _queue.Dequeue().Item;
            return true;
        }

        private readonly struct Entry
        {
            public Entry(T item, long releaseMs)
            {
                Item = item;
                ReleaseMs = releaseMs;
            }

            public T Item { get; }

            public long ReleaseMs { get; }
        }
    }
}
