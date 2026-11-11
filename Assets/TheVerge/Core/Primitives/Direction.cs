using System;

namespace TheVerge.Core.Primitives
{
    /// <summary>
    /// 8 направлений UO и флаг бега. Номера идут в пакетах как есть (0x02, 0x21, 0x20, 0x77, 0x78).
    /// Сервер: Projects/Server/Mobiles/Mobile.cs, enum Direction (там маска называется Mask).
    /// </summary>
    [Flags]
    public enum Direction : byte
    {
        North = 0x00,
        Right = 0x01,
        East = 0x02,
        Down = 0x03,
        South = 0x04,
        Left = 0x05,
        West = 0x06,
        Up = 0x07,

        FacingMask = 0x07,
        Running = 0x80
    }
}
