using System.Collections.Generic;
using TheVerge.Core.Multis;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>Карта из памяти: земля (по умолчанию ровная) и статика по клеткам; вне размеров — «вне карты».</summary>
    internal sealed class FakeMap : IMapData
    {
        private readonly Dictionary<long, LandTile> _land = new Dictionary<long, LandTile>();
        private readonly Dictionary<long, List<StaticTile>> _statics = new Dictionary<long, List<StaticTile>>();
        private readonly LandTile _default;

        public FakeMap(int width, int height, LandTile defaultLand)
        {
            Width = width;
            Height = height;
            _default = defaultLand;
        }

        public int Width { get; }
        public int Height { get; }

        public void SetLand(int x, int y, LandTile tile) => _land[Key(x, y)] = tile;

        public void AddStatic(int x, int y, StaticTile tile)
        {
            if (!_statics.TryGetValue(Key(x, y), out var list))
            {
                _statics[Key(x, y)] = list = new List<StaticTile>();
            }

            list.Add(tile);
        }

        public bool TryGetLand(int x, int y, out LandTile tile)
        {
            tile = default;
            if (!Contains(x, y))
            {
                return false;
            }

            tile = _land.TryGetValue(Key(x, y), out var set) ? set : _default;
            return true;
        }

        public bool TryGetStatics(int x, int y, List<StaticTile> result)
        {
            result.Clear();
            if (!Contains(x, y))
            {
                return false;
            }

            if (_statics.TryGetValue(Key(x, y), out var list))
            {
                result.AddRange(list);
            }

            return true;
        }

        private bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
