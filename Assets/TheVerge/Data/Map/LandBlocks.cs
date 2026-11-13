using System;
using System.IO;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;

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
        private readonly LandTile[][] _tiles;
        private readonly BlockSlots _slots;
        private readonly byte[] _buffer = new byte[CellsPerBlock * CellSize];

        public LandBlocks(MapDefinition map, Stream stream, LandBlockLocator locator, int capacity)
        {
            _map = map;
            _stream = stream;
            _locator = locator;
            _tiles = new LandTile[capacity][];
            _slots = new BlockSlots(capacity);
            for (var i = 0; i < capacity; i++)
            {
                _tiles[i] = new LandTile[CellsPerBlock];
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
            if (!_slots.Find(block, out var slot))
            {
                Read(block, _tiles[slot]);
            }

            return _tiles[slot];
        }

        private void Read(int block, LandTile[] tiles)
        {
            var offset = _locator.Offset(block);
            var read = 0;
            if (offset >= 0)
            {
                _stream.Position = offset;
                read = StreamRead.Fully(_stream, _buffer);
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
