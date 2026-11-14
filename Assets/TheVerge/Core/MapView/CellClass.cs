namespace TheVerge.Core.MapView
{
    /// <summary>Цвет клетки отладочной карты по флагам tiledata: от менее важного для проходимости к более важному.</summary>
    public enum CellClass : byte
    {
        Other = 0,
        Surface = 1,
        Blocked = 2,
        Wall = 3,
        Water = 4
    }
}
