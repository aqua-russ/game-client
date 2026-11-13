using System;
using TheVerge.Core.Ports;
using TheVerge.Data.Files;
using TheVerge.Data.Map;

namespace TheVerge.Bot
{
    /// <summary>Файлы данных, которые бот прочитал по --data: что найдено, tiledata, открытая карта, дома и корабли.</summary>
    public sealed class BotWorldData : IDisposable
    {
        public BotWorldData(DataLayout layout, ITileData tiles, MapData map, IMultiData multis)
        {
            Layout = layout;
            Tiles = tiles;
            Map = map;
            Multis = multis;
        }

        public DataLayout Layout { get; }
        public ITileData Tiles { get; }
        public MapData Map { get; }
        public IMultiData Multis { get; }

        public void Dispose() => Map.Dispose();
    }
}
