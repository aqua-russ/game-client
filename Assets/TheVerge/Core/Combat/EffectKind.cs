namespace TheVerge.Core.Combat
{
    /// <summary>
    /// Вид эффекта: числа — как EffectType сервера (Projects/Server/Effects.cs: Moving, Lightning, FixedXYZ, FixedFrom).
    /// <see cref="Screen"/> — экранный эффект 0x70 (SendScreenEffect: вид 4, номер экранного эффекта на месте графики).
    /// </summary>
    public enum EffectKind : byte
    {
        Moving = 0,
        Lightning = 1,
        FixedXYZ = 2,
        FixedFrom = 3,
        Screen = 4,
    }
}
