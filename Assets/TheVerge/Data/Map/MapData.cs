using System;
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

        public MapData(MapDefinition map, LandBlocks land)
        {
            _map = map;
            _land = land;
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

            return new MapData(files.Definition, new LandBlocks(files.Definition, stream, locator, cacheBlocks));
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

        public void Dispose() => _land.Dispose();

        private bool Contains(int x, int y) => x >= 0 && y >= 0 && x < _map.Width && y < _map.Height;

        private static LandBlockLocator Mul(out string error)
        {
            error = string.Empty;
            return LandBlockLocator.Mul();
        }
    }
}
