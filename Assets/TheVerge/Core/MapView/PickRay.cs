namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Луч для выбора клетки. Координаты — как у блоков карты: x на восток и y на юг в клетках, Z в единицах сервера
    /// (перевод из сцены Unity — только в Presentation, <c>CellSpace</c>). Направление не обязано быть единичным.
    /// </summary>
    public readonly struct PickRay
    {
        public PickRay(double originX, double originY, double originZ, double dirX, double dirY, double dirZ)
        {
            OriginX = originX;
            OriginY = originY;
            OriginZ = originZ;
            DirX = dirX;
            DirY = dirY;
            DirZ = dirZ;
        }

        public double OriginX { get; }
        public double OriginY { get; }
        public double OriginZ { get; }
        public double DirX { get; }
        public double DirY { get; }
        public double DirZ { get; }
    }
}
