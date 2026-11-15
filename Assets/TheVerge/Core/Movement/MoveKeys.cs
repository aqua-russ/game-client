using System;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Клавиши движения по смыслу на экране (CONTROLS §4): W — вверх, D — вправо, S — вниз, A — влево. Какая физическая
    /// клавиша что значит, решает слой ввода Unity; Core знает только смысл. Флаги: набор зажатых клавиш — одно число.
    /// </summary>
    [Flags]
    public enum MoveKeys : byte
    {
        None = 0,
        Up = 0x1,
        Right = 0x2,
        Down = 0x4,
        Left = 0x8
    }
}
