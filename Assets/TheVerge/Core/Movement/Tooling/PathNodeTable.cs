using System;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.Movement.Tooling
{
    /// <summary>
    /// Pre-allocated node pool and stamp-reset open-addressing hash table for <see cref="PathFinder"/>.
    /// Zero allocations in steady state across repeated searches.
    /// </summary>
    internal sealed class PathNodeTable
    {
        public const byte StateOpen = 1;
        public const byte StateClosed = 2;

        private readonly Node[] _nodes;
        private readonly long[] _slotKeys;
        private readonly int[] _slotNodes;
        private readonly int[] _slotStamps;
        private readonly int _mask;
        private int _stamp = 1;
        private int _count;

        public PathNodeTable(int capacity)
        {
            _nodes = new Node[capacity];
            int hashSize = NextPowerOfTwo(capacity * 2);
            _slotKeys = new long[hashSize];
            _slotNodes = new int[hashSize];
            _slotStamps = new int[hashSize];
            _mask = hashSize - 1;
        }

        public int Count => _count;

        public void Clear()
        {
            _count = 0;
            if (_stamp == int.MaxValue)
            {
                Array.Clear(_slotStamps, 0, _slotStamps.Length);
                _stamp = 1;
            }
            else
            {
                _stamp++;
            }
        }

        public Point3D PositionOf(int index) => _nodes[index].Position;

        public int GCostOf(int index) => _nodes[index].GCost;

        public int ParentOf(int index) => _nodes[index].ParentIndex;

        public Direction StepOf(int index) => _nodes[index].StepFromParent;

        public byte StateOf(int index) => _nodes[index].State;

        public int HeapIndexOf(int index) => _nodes[index].HeapIndex;

        public void SetHeapIndex(int index, int heapIndex) => _nodes[index].HeapIndex = heapIndex;

        public void MarkClosed(int index)
        {
            _nodes[index].State = StateClosed;
            _nodes[index].HeapIndex = -1;
        }

        public bool IsHigherPriority(int a, int b)
        {
            int fa = _nodes[a].FCost;
            int fb = _nodes[b].FCost;
            return fa < fb || (fa == fb && _nodes[a].HCost < _nodes[b].HCost);
        }

        public bool TryFind(Point3D pos, out int nodeIndex)
        {
            int slot = FindSlot(PackKey(pos), out bool found);
            nodeIndex = found ? _slotNodes[slot] : -1;
            return found;
        }

        public int TryAddOpen(Point3D pos, int parentIndex, Direction step, int gCost, int hCost)
        {
            if (_count >= _nodes.Length)
            {
                return -1;
            }

            long key = PackKey(pos);
            int slot = FindSlot(key, out _);
            int index = _count++;
            _nodes[index] = new Node
            {
                Position = pos,
                GCost = gCost,
                HCost = hCost,
                FCost = gCost + hCost,
                ParentIndex = parentIndex,
                HeapIndex = -1,
                StepFromParent = step,
                State = StateOpen,
            };
            _slotStamps[slot] = _stamp;
            _slotKeys[slot] = key;
            _slotNodes[slot] = index;
            return index;
        }

        public void UpdateOpen(int nodeIndex, int parentIndex, Direction step, int gCost)
        {
            _nodes[nodeIndex].ParentIndex = parentIndex;
            _nodes[nodeIndex].StepFromParent = step;
            _nodes[nodeIndex].GCost = gCost;
            _nodes[nodeIndex].FCost = gCost + _nodes[nodeIndex].HCost;
        }

        private int FindSlot(long key, out bool found)
        {
            int slot = Bucket(key);
            while (_slotStamps[slot] == _stamp)
            {
                if (_slotKeys[slot] == key)
                {
                    found = true;
                    return slot;
                }

                slot = (slot + 1) & _mask;
            }

            found = false;
            return slot;
        }

        private int Bucket(long key)
        {
            ulong x = (ulong)key;
            x ^= x >> 30;
            x *= 0xbf58476d1ce4e5b9UL;
            x ^= x >> 27;
            x *= 0x94d049bb133111ebUL;
            x ^= x >> 31;
            return (int)(x & (ulong)_mask);
        }

        private static long PackKey(Point3D p) =>
            ((long)(ushort)p.X) | (((long)(ushort)p.Y) << 16) | (((long)(byte)p.Z) << 32);

        private static int NextPowerOfTwo(int min)
        {
            int value = 16;
            while (value < min)
            {
                value <<= 1;
            }

            return value;
        }

        private struct Node
        {
            public Point3D Position;
            public int GCost;
            public int HCost;
            public int FCost;
            public int ParentIndex;
            public int HeapIndex;
            public Direction StepFromParent;
            public byte State;
        }
    }
}
