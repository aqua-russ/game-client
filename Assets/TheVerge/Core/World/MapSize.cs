namespace TheVerge.Core.World
{
    /// <summary>Ширина и высота карты в клетках (Фелукка — 7168×4096, docs/TARGET.md §1).</summary>
    public readonly struct MapSize
    {
        public MapSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public override string ToString() => Width + "×" + Height;
    }
}
