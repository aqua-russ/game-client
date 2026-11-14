using System;
using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Блоки отладочной 3D-карты вокруг клетки (PLAN Стадия 2, п. 13): земля, статика и части домов и кораблей — только из
    /// данных (<see cref="IMapData"/>, <see cref="ITileData"/>, <see cref="IMultiData"/>) и мира клиента. Мобы и предметы
    /// на земле — не блоки, их показывает Presentation по событиям мира. Верх блока там, где его видит сервер: Z земли;
    /// Z статики плюс высота плитки (<see cref="ItemTileData.CalcHeight"/>). Земля — плоский блок на Z клетки, наклон
    /// между углами (нужен проверке шага, Стадия 3) здесь не рисуется.
    /// </summary>
    public sealed class SceneBuilder
    {
        private readonly IMapData _map;
        private readonly ITileData _tiles;
        private readonly MultiBlocks _multis;
        private readonly List<StaticTile> _statics = new List<StaticTile>();
        private LandTile[] _land = Array.Empty<LandTile>();
        private bool[] _present = Array.Empty<bool>();

        public SceneBuilder(IMapData map, ITileData tiles, IMultiData multis, EntityStore world)
        {
            _map = map;
            _tiles = tiles;
            _multis = new MultiBlocks(tiles, multis, world);
        }

        /// <summary>Блоки квадрата |dx| ≤ radius, |dy| ≤ radius вокруг клетки; <paramref name="result"/> очищается.</summary>
        public void Build(int centerX, int centerY, int radius, List<SceneBlock> result)
        {
            result.Clear();
            var size = 2 * radius + 3;
            var x0 = centerX - radius - 1;
            var y0 = centerY - radius - 1;
            ReadLand(x0, y0, size);
            for (var gy = 1; gy < size - 1; gy++)
            {
                for (var gx = 1; gx < size - 1; gx++)
                {
                    AddCell(x0 + gx, y0 + gy, gy * size + gx, size, result);
                }
            }

            _multis.Add(centerX, centerY, radius, result);
        }

        // Земля читается с запасным кольцом в одну клетку: низ блока зависит от высоты соседей.
        private void ReadLand(int x0, int y0, int size)
        {
            var count = size * size;
            if (_land.Length < count)
            {
                _land = new LandTile[count];
                _present = new bool[count];
            }

            for (var gy = 0; gy < size; gy++)
            {
                for (var gx = 0; gx < size; gx++)
                {
                    _present[gy * size + gx] = _map.TryGetLand(x0 + gx, y0 + gy, out _land[gy * size + gx]);
                }
            }
        }

        private void AddCell(int x, int y, int index, int size, List<SceneBlock> result)
        {
            if (!_present[index])
            {
                return;
            }

            var land = _land[index];
            var bottom = Math.Min(LowestNeighbour(index, size, land.Z), land.Z - SceneLimits.LandMinThickness);
            var flags = _tiles.Land(land.Id).Flags;
            result.Add(new SceneBlock(SceneKind.Land, x, y, bottom, land.Z, CellClassifier.Classify(flags, true)));
            _map.TryGetStatics(x, y, _statics);
            foreach (var tile in _statics)
            {
                var data = _tiles.Item(tile.Id);
                result.Add(MultiBlocks.Block(SceneKind.Static, x, y, tile.Z, data));
            }
        }

        private int LowestNeighbour(int index, int size, int z)
        {
            var lowest = z;
            lowest = Lower(lowest, index - 1);
            lowest = Lower(lowest, index + 1);
            lowest = Lower(lowest, index - size);
            return Lower(lowest, index + size);
        }

        private int Lower(int current, int neighbour) =>
            _present[neighbour] && _land[neighbour].Z < current ? _land[neighbour].Z : current;
    }
}
