namespace TheVerge.Core.Movement.Tooling
{
    /// <summary>
    /// Pre-allocated binary min-heap over <see cref="PathNodeTable"/> indices supporting in-place DecreaseKey.
    /// </summary>
    internal sealed class PathMinHeap
    {
        private readonly PathNodeTable _table;
        private readonly int[] _items;
        private int _count;

        public PathMinHeap(PathNodeTable table, int capacity)
        {
            _table = table;
            _items = new int[capacity];
        }

        public int Count => _count;

        public void Clear() => _count = 0;

        public void Push(int nodeIndex)
        {
            int pos = _count++;
            _items[pos] = nodeIndex;
            _table.SetHeapIndex(nodeIndex, pos);
            SiftUp(pos);
        }

        public int Pop()
        {
            int top = _items[0];
            _count--;
            if (_count > 0)
            {
                int moved = _items[_count];
                _items[0] = moved;
                _table.SetHeapIndex(moved, 0);
                SiftDown(0);
            }

            _table.SetHeapIndex(top, -1);
            return top;
        }

        public void DecreaseKey(int nodeIndex)
        {
            int pos = _table.HeapIndexOf(nodeIndex);
            if (pos >= 0)
            {
                SiftUp(pos);
            }
        }

        private void SiftUp(int pos)
        {
            int node = _items[pos];
            while (pos > 0)
            {
                int parentPos = (pos - 1) >> 1;
                int parentNode = _items[parentPos];
                if (!_table.IsHigherPriority(node, parentNode))
                {
                    break;
                }

                _items[pos] = parentNode;
                _table.SetHeapIndex(parentNode, pos);
                pos = parentPos;
            }

            _items[pos] = node;
            _table.SetHeapIndex(node, pos);
        }

        private void SiftDown(int pos)
        {
            int node = _items[pos];
            int half = _count >> 1;
            while (pos < half)
            {
                int bestChild = (pos << 1) + 1;
                int right = bestChild + 1;
                if (right < _count && _table.IsHigherPriority(_items[right], _items[bestChild]))
                {
                    bestChild = right;
                }

                int childNode = _items[bestChild];
                if (!_table.IsHigherPriority(childNode, node))
                {
                    break;
                }

                _items[pos] = childNode;
                _table.SetHeapIndex(childNode, pos);
                pos = bestChild;
            }

            _items[pos] = node;
            _table.SetHeapIndex(node, pos);
        }
    }
}
