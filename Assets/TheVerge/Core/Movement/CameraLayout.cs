namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Как камера показывает карту — от этого зависит, какое направление UO даёт клавиша (PLAN Стадия 3, п. 1).
    /// «Север сверху» — отладочная камера; изометрия — как в классическом клиенте (север — вверх-вправо).
    /// </summary>
    public enum CameraLayout : byte
    {
        NorthUp = 0,
        Isometric = 1
    }
}
