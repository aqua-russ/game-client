namespace TheVerge.Core.Tiles
{
    /// <summary>Свойства плитки предмета (статики, предметы, части домов) из tiledata — как ItemData сервера (TileData.cs).</summary>
    public readonly struct ItemTileData
    {
        private readonly ItemTileProperties _properties;

        public ItemTileData(string name, TileFlags flags, ItemTileProperties properties)
        {
            Name = name;
            Flags = flags;
            _properties = properties;
        }

        public string Name { get; }
        public TileFlags Flags { get; }
        public int Weight => _properties.Weight;

        /// <summary>У надеваемых — слой; так же это поле называет и использует сервер.</summary>
        public int Quality => _properties.Quality;

        public int Animation => _properties.Animation;
        public int Quantity => _properties.Quantity;
        public int Value => _properties.Value;
        public int Height => _properties.Height;

        /// <summary>Высота для расчёта Z: у мостов (Bridge) — половина (ItemData.CalcHeight сервера).</summary>
        public int CalcHeight => Has(TileFlags.Bridge) ? Height / 2 : Height;

        public bool Has(TileFlags flag) => (Flags & flag) != 0;
    }
}
