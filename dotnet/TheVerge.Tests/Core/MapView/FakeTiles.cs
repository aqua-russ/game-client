using System.Collections.Generic;
using TheVerge.Core.Multis;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>Свойства плиток из памяти: что не задано — пустая запись без флагов и высоты.</summary>
    internal sealed class FakeTiles : ITileData
    {
        private readonly Dictionary<int, LandTileData> _land = new Dictionary<int, LandTileData>();
        private readonly Dictionary<int, ItemTileData> _items = new Dictionary<int, ItemTileData>();

        public int ItemCount => 0x10000;

        public void SetLand(int id, TileFlags flags, string name = "land") => _land[id] = new LandTileData(name, flags);

        public void SetItem(int id, TileFlags flags, byte height, string name = "item") =>
            _items[id] = new ItemTileData(name, flags, new ItemTileProperties(0, 0, 0, 0, 0, height));

        public LandTileData Land(int id) => _land.TryGetValue(id, out var data) ? data : new LandTileData("", TileFlags.None);

        public ItemTileData Item(int id) =>
            _items.TryGetValue(id, out var data) ? data : new ItemTileData("", TileFlags.None, new ItemTileProperties(0, 0, 0, 0, 0, 0));
    }
}
