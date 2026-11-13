namespace TheVerge.Core.Tiles
{
    /// <summary>Свойства плитки земли из tiledata: имя и флаги (как LandData сервера, TileData.cs).</summary>
    public readonly struct LandTileData
    {
        public LandTileData(string name, TileFlags flags)
        {
            Name = name;
            Flags = flags;
        }

        public string Name { get; }
        public TileFlags Flags { get; }

        public bool Has(TileFlags flag) => (Flags & flag) != 0;
    }
}
