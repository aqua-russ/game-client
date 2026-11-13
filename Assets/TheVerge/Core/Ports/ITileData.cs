using TheVerge.Core.Tiles;

namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Свойства плиток (tiledata). Реализация — Data/Tiles/TileDataTable; Core не знает, из какого файла и формата.
    /// Номер берётся по маске, как у сервера (TileData.LandTable[id &amp; MaxLandValue], ItemTable[id &amp; MaxItemValue]):
    /// земли 0x4000, предметов 0x10000; номера, которых нет в файле, — пустые записи.
    /// </summary>
    public interface ITileData
    {
        /// <summary>Сколько предметов в файле: 0x4000, 0x8000 или 0x10000 (зависит от формата).</summary>
        int ItemCount { get; }

        LandTileData Land(int id);

        ItemTileData Item(int id);
    }
}
