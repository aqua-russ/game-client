using System.Collections.Generic;

namespace TheVerge.Data.Map
{
    /// <summary>
    /// Места кэша блоков карты: какой блок в каком месте и когда к нему обращались. Новый блок занимает свободное
    /// место или вытесняет тот, к которому дольше всех не обращались. Обращение к блоку в кэше новой памяти не требует.
    /// </summary>
    public sealed class BlockSlots
    {
        private readonly int[] _blockOfSlot;
        private readonly long[] _lastUse;
        private readonly Dictionary<int, int> _slotOfBlock;
        private long _clock;

        public BlockSlots(int capacity)
        {
            _blockOfSlot = new int[capacity];
            _lastUse = new long[capacity];
            _slotOfBlock = new Dictionary<int, int>(capacity);
            for (var i = 0; i < capacity; i++)
            {
                _blockOfSlot[i] = -1;
            }
        }

        public int Capacity => _blockOfSlot.Length;

        /// <summary>true — блок уже в кэше; false — блоку отдано место <paramref name="slot"/>, его надо прочитать.</summary>
        public bool Find(int block, out int slot)
        {
            var found = _slotOfBlock.TryGetValue(block, out slot);
            if (!found)
            {
                slot = Evict();
                _blockOfSlot[slot] = block;
                _slotOfBlock[block] = slot;
            }

            _lastUse[slot] = ++_clock;
            return found;
        }

        private int Evict()
        {
            var oldest = 0;
            for (var i = 0; i < _blockOfSlot.Length; i++)
            {
                if (_blockOfSlot[i] < 0)
                {
                    return i;
                }

                if (_lastUse[i] < _lastUse[oldest])
                {
                    oldest = i;
                }
            }

            _slotOfBlock.Remove(_blockOfSlot[oldest]);
            return oldest;
        }
    }
}
