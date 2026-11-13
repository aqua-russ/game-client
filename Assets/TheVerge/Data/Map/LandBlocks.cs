using System;
using System.Collections.Generic;
using System.IO;
using TheVerge.Core.Tiles;

namespace TheVerge.Data.Map
{
    /// <summary>
    /// Блоки земли 8×8 с кэшем недавних (PLAN Стадия 2, принципы: карту не читаем целиком). Блок — 64 клетки по 3 байта:
    /// номер (short) и Z (sbyte), клетка (x, y) — под номером y × 8 + x (TileMatrix.GetLandTile). Блока нет в файле или
    /// файл кончился — недочитанные клетки пустые, как у сервера. Вытесняется блок, к которому дольше всех не обращались.
    /// </summary>
    public sealed class LandBlocks : IDisposable
    {
        private const int CellsPerBlock = 64;
        private const int CellSize = 3;

        private readonly MapDefinition _map;
        private readonly Stream _stream;
        private readonly LandBlockLocator _locator;
        private readonly LandTile[][] _slots;
        private readonly int[] _blockOfSlot;
        private readonly long[] _lastUse;
        private readonly Dictionary<int, int> _slotOfBlock;
        private readonly byte[] _buffer = new byte[CellsPerBlock * CellSize];
        private long _clock;

        public LandBlocks(MapDefinition map, Stream stream, LandBlockLocator locator, int capacity)
        {
            _map = map;
            _stream = stream;
            _locator = locator;
            _slots = new LandTile[capacity][];
            _blockOfSlot = new int[capacity];
            _lastUse = new long[capacity];
            _slotOfBlock = new Dictionary<int, int>(capacity);
            for (var i = 0; i < capacity; i++)
            {
                _slots[i] = new LandTile[CellsPerBlock];
                _blockOfSlot[i] = -1;
            }
        }

        public MapDefinition Map => _map;

        /// <summary>Клетка внутри карты (проверяет вызывающий).</summary>
        public LandTile Get(int x, int y)
        {
            var block = (x >> MapDefinition.BlockShift) * _map.BlockHeight + (y >> MapDefinition.BlockShift);
            return Block(block)[((y & 7) << 3) + (x & 7)];
        }

        public void Dispose() => _stream.Dispose();

        private LandTile[] Block(int block)
        {
            if (!_slotOfBlock.TryGetValue(block, out var slot))
            {
                slot = Evict();
                Read(block, _slots[slot]);
                _blockOfSlot[slot] = block;
                _slotOfBlock[block] = slot;
            }

            _lastUse[slot] = ++_clock;
            return _slots[slot];
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

        private void Read(int block, LandTile[] tiles)
        {
            var offset = _locator.Offset(block);
            var read = 0;
            if (offset >= 0)
            {
                _stream.Position = offset;
                read = Files.UopHeader.ReadFully(_stream, _buffer);
            }

            Array.Clear(_buffer, read, _buffer.Length - read);
            for (var i = 0; i < CellsPerBlock; i++)
            {
                var at = i * CellSize;
                tiles[i] = new LandTile((short)(_buffer[at] | (_buffer[at + 1] << 8)), (sbyte)_buffer[at + 2]);
            }
        }
    }
}
