using System;

namespace TheVerge.Core
{
    /// <summary>
    /// 8 направлений взгляда/движения протокола UO с битовым флагом бега.
    /// </summary>
    [Flags]
    public enum Direction : byte
    {
        North = 0x00,
        Right = 0x01,
        East  = 0x02,
        Down  = 0x03,
        South = 0x04,
        Left  = 0x05,
        West  = 0x06,
        Up    = 0x07,

        FacingMask = 0x07,
        Running    = 0x80
    }
}
