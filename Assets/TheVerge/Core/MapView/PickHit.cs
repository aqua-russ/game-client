namespace TheVerge.Core.MapView
{
    /// <summary>Куда попал луч: клетка, вид блока и расстояние вдоль луча (в длинах его направления) до входа в блок.</summary>
    public readonly struct PickHit
    {
        public PickHit(int x, int y, SceneKind kind, double distance)
        {
            X = x;
            Y = y;
            Kind = kind;
            Distance = distance;
        }

        public int X { get; }
        public int Y { get; }
        public SceneKind Kind { get; }
        public double Distance { get; }
    }
}
