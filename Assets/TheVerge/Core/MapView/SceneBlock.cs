namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Прямоугольный блок отладочной карты на одной клетке: от <see cref="ZBottom"/> до <see cref="ZTop"/> в единицах Z
    /// сервера. Координаты — клетки UO (x на восток, y на юг); перевод в сцену — только в Presentation.
    /// </summary>
    public readonly struct SceneBlock
    {
        public SceneBlock(SceneKind kind, int x, int y, int zBottom, int zTop, CellClass cellClass)
        {
            Kind = kind;
            X = x;
            Y = y;
            ZBottom = zBottom;
            ZTop = zTop;
            Class = cellClass;
        }

        public SceneKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int ZBottom { get; }
        public int ZTop { get; }
        public CellClass Class { get; }
    }
}
