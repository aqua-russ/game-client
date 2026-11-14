using System;
using System.IO;
using TheVerge.Core.Ports;
using TheVerge.Data.Files;
using TheVerge.Data.Map;
using TheVerge.Data.Multis;
using TheVerge.Data.Tiles;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Файлы данных мира, прочитанные для отладочной карты: свойства плиток, карта (только Фелукка — TARGET.md §1) и дома
    /// с кораблями. Читает их Data (Presentation о файлах не знает, AGENTS.md §2.1), наружу — порты Core. То же делает бот
    /// по --data (BotData).
    /// </summary>
    public sealed class UnityWorldData : IDisposable
    {
        private readonly MapData _map;

        private UnityWorldData(ITileData tiles, MapData map, IMultiData multis)
        {
            Tiles = tiles;
            _map = map;
            Multis = multis;
        }

        public ITileData Tiles { get; }

        public IMapData Map => _map;

        public IMultiData Multis { get; }

        /// <summary>null и <paramref name="error"/> словами — папка или файл не подходят.</summary>
        public static UnityWorldData Load(string folder, ILog log, out string error)
        {
            try
            {
                return LoadFiles(folder, log, out error);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                log.Error("Файлы данных не прочитаны: " + e);
                return null;
            }
        }

        public void Dispose() => _map.Dispose();

        private static UnityWorldData LoadFiles(string folder, ILog log, out string error)
        {
            var detection = DataLayoutDetector.Detect(folder, MapDefinition.Felucca, new DataOptions());
            if (!detection.IsValid)
            {
                error = detection.Error;
                return null;
            }

            var layout = detection.Layout;
            foreach (var line in layout.Describe())
            {
                log.Info(line);
            }

            var tiles = TileDataReader.Load(layout.TileData, out error);
            if (tiles == null)
            {
                return null;
            }

            var map = MapData.Open(layout.Map, MapData.DefaultCacheBlocks, out error);
            if (map == null)
            {
                return null;
            }

            var multis = MultiTable.Load(layout.Multi, out error);
            if (multis == null)
            {
                map.Dispose();
                return null;
            }

            return new UnityWorldData(tiles, map, multis);
        }
    }
}
