using System.Collections.Generic;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Объекты по блокам карты 8×8 клеток (как блоки карты UO): поиск в радиусе смотрит только блоки, которые задевает
    /// квадрат. Пустые списки блоков остаются: объекты ходят туда-сюда, а новая память на каждом шаге не нужна.
    /// </summary>
    internal sealed class BlockGrid<T> where T : class
    {
        private const int BlockShift = 3;

        private readonly Dictionary<long, List<Entry>> _blocks = new Dictionary<long, List<Entry>>();

        public void Add(T value, short x, short y)
        {
            var key = Key(x >> BlockShift, y >> BlockShift);
            if (!_blocks.TryGetValue(key, out var list))
            {
                list = new List<Entry>();
                _blocks.Add(key, list);
            }

            list.Add(new Entry(value, x, y));
        }

        public void Remove(T value, short x, short y)
        {
            if (!_blocks.TryGetValue(Key(x >> BlockShift, y >> BlockShift), out var list))
            {
                return;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i].Value, value))
                {
                    list[i] = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);
                    return;
                }
            }
        }

        public void Clear() => _blocks.Clear();

        /// <summary>Всё в квадрате |dx| ≤ radius, |dy| ≤ radius вокруг (x, y) — в <paramref name="results"/> (он очищается).</summary>
        public void Query(int x, int y, int radius, List<T> results)
        {
            results.Clear();
            if (radius < 0)
            {
                return;
            }

            // Клетки — short: за его пределами искать нечего, а long не даёт переполниться на огромном радиусе.
            var minX = (int)System.Math.Max((long)x - radius, short.MinValue);
            var maxX = (int)System.Math.Min((long)x + radius, short.MaxValue);
            var minY = (int)System.Math.Max((long)y - radius, short.MinValue);
            var maxY = (int)System.Math.Min((long)y + radius, short.MaxValue);
            var blocks = ((long)(maxX >> BlockShift) - (minX >> BlockShift) + 1) * ((long)(maxY >> BlockShift) - (minY >> BlockShift) + 1);
            if (blocks > _blocks.Count)
            {
                foreach (var list in _blocks.Values)
                {
                    Collect(list, minX, maxX, minY, maxY, results);
                }

                return;
            }

            for (var bx = minX >> BlockShift; bx <= maxX >> BlockShift; bx++)
            {
                for (var by = minY >> BlockShift; by <= maxY >> BlockShift; by++)
                {
                    if (_blocks.TryGetValue(Key(bx, by), out var list))
                    {
                        Collect(list, minX, maxX, minY, maxY, results);
                    }
                }
            }
        }

        private static void Collect(List<Entry> list, int minX, int maxX, int minY, int maxY, List<T> results)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry.X >= minX && entry.X <= maxX && entry.Y >= minY && entry.Y <= maxY)
                {
                    results.Add(entry.Value);
                }
            }
        }

        private static long Key(int blockX, int blockY) => ((long)blockX << 32) | (uint)blockY;

        private readonly struct Entry
        {
            public Entry(T value, short x, short y)
            {
                Value = value;
                X = x;
                Y = y;
            }

            public T Value { get; }

            public short X { get; }

            public short Y { get; }
        }
    }
}
