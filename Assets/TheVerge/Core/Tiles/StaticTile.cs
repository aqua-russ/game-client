namespace TheVerge.Core.Tiles
{
    /// <summary>
    /// Статика на клетке: номер плитки предмета, высота и цвет — как StaticTile сервера (TileMatrix.cs): номер без
    /// знака, Z и цвет со знаком. Свойства — по номеру в <see cref="Ports.ITileData.Item"/>.
    /// </summary>
    public readonly struct StaticTile
    {
        public StaticTile(int id, int z, int hue)
        {
            Id = id;
            Z = z;
            Hue = hue;
        }

        public int Id { get; }
        public int Z { get; }
        public int Hue { get; }
    }
}
