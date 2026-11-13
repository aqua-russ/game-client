namespace TheVerge.Data.Map
{
    /// <summary>
    /// Карта: номер, номер файлов (map{N}, staidx{N}, statics{N}) и размер в клетках. Значения — из
    /// Distribution/Data/map-definitions.json сервера; в эре UOR используется только Фелукка (TARGET.md §1).
    /// </summary>
    public readonly struct MapDefinition
    {
        /// <summary>Карта сторонами делится на блоки 8×8 клеток (TileMatrix.SectorShift = 3).</summary>
        public const int BlockShift = 3;

        public static readonly MapDefinition Felucca = new MapDefinition(0, 0, "Felucca", 7168, 4096);

        public MapDefinition(int index, int fileIndex, string name, int width, int height)
        {
            Index = index;
            FileIndex = fileIndex;
            Name = name;
            Width = width;
            Height = height;
        }

        public int Index { get; }
        public int FileIndex { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public int BlockWidth => Width >> BlockShift;
        public int BlockHeight => Height >> BlockShift;
        public long BlockCount => (long)BlockWidth * BlockHeight;
    }
}
