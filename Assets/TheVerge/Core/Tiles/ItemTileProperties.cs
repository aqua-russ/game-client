namespace TheVerge.Core.Tiles
{
    /// <summary>Числа записи предмета в tiledata, которые читает сервер (TileData.cs, ItemData): каждое — один байт, анимация — два.</summary>
    public readonly struct ItemTileProperties
    {
        public ItemTileProperties(byte weight, byte quality, ushort animation, byte quantity, byte value, byte height)
        {
            Weight = weight;
            Quality = quality;
            Animation = animation;
            Quantity = quantity;
            Value = value;
            Height = height;
        }

        public byte Weight { get; }

        /// <summary>У надеваемых предметов — слой (Layer), на который предмет надевается.</summary>
        public byte Quality { get; }

        public ushort Animation { get; }
        public byte Quantity { get; }
        public byte Value { get; }
        public byte Height { get; }
    }
}
