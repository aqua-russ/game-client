using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;

namespace TheVerge.Data.Tiles
{
    /// <summary>
    /// Таблица tiledata в памяти (файл небольшой — до 3 МБ, сервер тоже держит его целиком). Земли 0x4000, предметов
    /// 0x10000 при любом формате файла — как у сервера (TileData.LandTable, ItemTable); чего нет в файле — пусто.
    /// </summary>
    public sealed class TileDataTable : ITileData
    {
        public const int LandTableSize = 0x4000;
        public const int ItemTableSize = 0x10000;

        private readonly LandTileData[] _land;
        private readonly ItemTileData[] _items;

        public TileDataTable(LandTileData[] land, ItemTileData[] items, int itemCount)
        {
            _land = land;
            _items = items;
            ItemCount = itemCount;
        }

        public int ItemCount { get; }

        public LandTileData Land(int id) => _land[id & (LandTableSize - 1)];

        public ItemTileData Item(int id) => _items[id & (ItemTableSize - 1)];

        /// <summary>Заменить запись предмета — только для правок сервера (<see cref="ServerTileDataFixes"/>).</summary>
        internal void SetItem(int id, ItemTileData item) => _items[id & (ItemTableSize - 1)] = item;
    }
}
