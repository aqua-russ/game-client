namespace TheVerge.Core.MapView
{
    /// <summary>Что о предмете или мобе на клетке говорит мир клиента: серийник, цвет, количество. У земли и статики — <see cref="None"/>.</summary>
    public readonly struct CellEntity
    {
        public CellEntity(uint serial, int hue, int amount)
        {
            Serial = serial;
            Hue = hue;
            Amount = amount;
        }

        public static CellEntity None => default;

        public uint Serial { get; }
        public int Hue { get; }
        public int Amount { get; }
    }
}
