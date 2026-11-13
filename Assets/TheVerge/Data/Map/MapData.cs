using System;
using System.Collections.Generic;
using System.IO;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;

namespace TheVerge.Data.Map
{
    /// <summary>
    /// Карта для Core (порт <see cref="IMapData"/>): земля — <see cref="LandBlocks"/>. Открывается по найденным файлам
    /// (<see cref="DataLayoutDetector"/>); сломанный файл — ошибка словами, не исключение.
    /// </summary>
    public sealed class MapData : IMapData, IDisposable
    {
        /// <summary>Блоков в кэше по умолчанию: обзор 18 клеток — около 49 блоков, запас на ходьбу и сверку.</summary>
        public const int DefaultCacheBlocks = 1024;

        private readonly MapDefinition _map;
        private readonly LandBlocks _land;
        private readonly StaticBlocks _statics;

        public MapData(MapDefinition map, LandBlocks land, StaticBlocks statics)
        {
            _map = map;
            _land = land;
            _statics = statics;
        }

        public int Width => _map.Width;
        public int Height => _map.Height;

        public static MapData? Open(MapFiles files, int cacheBlocks, out string error)
        {
            var stream = new FileStream(files.MapPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var locator = files.Kind == MapFileKind.Uop ? LandBlockLocator.Uop(stream, files.MapPath, out error) : Mul(out error);
            if (locator == null)
            {
                stream.Dispose();
                return null;
            }

            var index = new FileStream(files.IndexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var statics = new FileStream(files.StaticsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new MapData(files.Definition, new LandBlocks(files.Definition, stream, locator, cacheBlocks),
                new StaticBlocks(files.Definition, index, statics, cacheBlocks));
        }

        public bool TryGetLand(int x, int y, out LandTile tile)
        {
            if (!Contains(x, y))
            {
                tile = default;
                return false;
            }

            tile = _land.Get(x, y);
            return true;
        }

        public bool TryGetStatics(int x, int y, List<StaticTile> result)
        {
            result.Clear();
            if (!Contains(x, y))
            {
                return false;
            }

            _statics.Get(x, y, result);
            return true;
        }

        public void Dispose()
        {
            _land.Dispose();
            _statics.Dispose();
        }

        private bool Contains(int x, int y) => x >= 0 && y >= 0 && x < _map.Width && y < _map.Height;

        private static LandBlockLocator Mul(out string error)
        {
            error = string.Empty;
            return LandBlockLocator.Mul();
        }
    }
}
