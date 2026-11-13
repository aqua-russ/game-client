using System;
using TheVerge.Core.Ports;
using TheVerge.Data.Files;
using TheVerge.Data.Map;

namespace TheVerge.Bot
{
    /// <summary>Файлы данных, которые бот прочитал по --data: что найдено, tiledata и открытая карта.</summary>
    public sealed class BotWorldData : IDisposable
    {
        public BotWorldData(DataLayout layout, ITileData tiles, MapData map)
        {
            Layout = layout;
            Tiles = tiles;
            Map = map;
        }

        public DataLayout Layout { get; }
        public ITileData Tiles { get; }
        public MapData Map { get; }

        public void Dispose() => Map.Dispose();
    }
}
