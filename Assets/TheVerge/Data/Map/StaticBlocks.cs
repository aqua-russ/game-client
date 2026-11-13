using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;

namespace TheVerge.Data.Map
{
    /// <summary>
    /// Статика по блокам 8×8 с кэшем недавних, как у сервера (TileMatrix.ReadStaticBlock): staidx — на блок 12 байт
    /// (адрес, длина, ещё 4), адрес меньше 0 или длина 0 — пусто, индекс кончился — пусто; записей — длина / 7, каждая —
    /// номер, X, Y, Z, цвет; клетка записи — по младшим 3 битам X и Y, порядок внутри клетки — порядок файла.
    /// Отличие: если блок обрывается концом файла, сервер подставил бы остаток прошлого блока из общего буфера, а здесь
    /// берутся только целые записи — в настоящих файлах так не бывает.
    /// </summary>
    public sealed class StaticBlocks : IDisposable
    {
        private const int IndexRecordSize = 12;
        private const int RecordSize = 7;
        private const int Cells = 64;

        private readonly MapDefinition _map;
        private readonly Stream _index;
        private readonly Stream _data;
        private readonly BlockSlots _slots;
        private readonly StaticTile[][] _tiles;
        private readonly int[][] _cellStart;
        private readonly byte[] _indexBuffer = new byte[8];
        private byte[] _buffer = new byte[RecordSize * 64];
        private readonly int[] _next = new int[Cells];
        private int[] _keys = new int[64];

        public StaticBlocks(MapDefinition map, Stream index, Stream data, int capacity)
        {
            _map = map;
            _index = index;
            _data = data;
            _slots = new BlockSlots(capacity);
            _tiles = new StaticTile[capacity][];
            _cellStart = new int[capacity][];
            for (var i = 0; i < capacity; i++)
            {
                _tiles[i] = new StaticTile[0];
                _cellStart[i] = new int[Cells + 1];
            }
        }

        /// <summary>Статика клетки внутри карты (проверяет вызывающий) — в конец <paramref name="result"/>.</summary>
        public void Get(int x, int y, List<StaticTile> result)
        {
            var block = (x >> MapDefinition.BlockShift) * _map.BlockHeight + (y >> MapDefinition.BlockShift);
            if (!_slots.Find(block, out var slot))
            {
                Read(block, slot);
            }

            var cell = Cell(x, y);
            var tiles = _tiles[slot];
            var start = _cellStart[slot];
            for (var i = start[cell]; i < start[cell + 1]; i++)
            {
                result.Add(tiles[i]);
            }
        }

        public void Dispose()
        {
            _index.Dispose();
            _data.Dispose();
        }

        private static int Cell(int x, int y) => ((x & 7) << 3) | (y & 7);

        private void Read(int block, int slot)
        {
            var count = ReadRecords(block);
            Array.Clear(_cellStart[slot], 0, Cells + 1);
            if (_tiles[slot].Length < count)
            {
                _tiles[slot] = new StaticTile[count];
            }

            if (_keys.Length < count)
            {
                _keys = new int[count];
            }

            // Раскладка по клеткам с сохранением порядка файла: сначала число записей в каждой клетке, потом места.
            for (var i = 0; i < count; i++)
            {
                _keys[i] = Cell(_buffer[i * RecordSize + 2], _buffer[i * RecordSize + 3]);
                _cellStart[slot][_keys[i] + 1]++;
            }

            for (var cell = 0; cell < Cells; cell++)
            {
                _cellStart[slot][cell + 1] += _cellStart[slot][cell];
            }

            Place(slot, count);
        }

        private void Place(int slot, int count)
        {
            Array.Copy(_cellStart[slot], _next, Cells);
            for (var i = 0; i < count; i++)
            {
                var at = i * RecordSize;
                _tiles[slot][_next[_keys[i]]++] = new StaticTile(
                    BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(at)), (sbyte)_buffer[at + 4],
                    BinaryPrimitives.ReadInt16LittleEndian(_buffer.AsSpan(at + 5)));
            }
        }

        /// <summary>Читает записи блока в буфер; возвращает, сколько целых записей прочитано.</summary>
        private int ReadRecords(int block)
        {
            _index.Position = (long)block * IndexRecordSize;
            if (StreamRead.Fully(_index, _indexBuffer) < _indexBuffer.Length)
            {
                return 0;
            }

            var lookup = BinaryPrimitives.ReadInt32LittleEndian(_indexBuffer);
            var length = BinaryPrimitives.ReadInt32LittleEndian(_indexBuffer.AsSpan(4));
            if (lookup < 0 || length <= 0)
            {
                return 0;
            }

            var wanted = length / RecordSize * RecordSize;
            if (_buffer.Length < wanted)
            {
                _buffer = new byte[wanted];
            }

            _data.Position = lookup;
            return StreamRead.Fully(_data, _buffer, wanted) / RecordSize;
        }
    }
}
