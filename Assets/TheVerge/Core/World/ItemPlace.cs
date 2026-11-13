namespace TheVerge.Core.World
{
    /// <summary>Где лежит предмет.</summary>
    public enum ItemPlace : byte
    {
        /// <summary>Заглушка: известен только как контейнер другого предмета, сам ещё не приходил.</summary>
        Unplaced = 0,
        World = 1,
        Container = 2,
        Equipped = 3
    }
}
