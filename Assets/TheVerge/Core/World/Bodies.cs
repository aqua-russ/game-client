namespace TheVerge.Core.World
{
    /// <summary>Свойства тела моба — как у сервера (Projects/Server/Mobiles/Body.cs).</summary>
    public static class Bodies
    {
        /// <summary>Тело призрака: Body.IsGhost сервера — 402, 403 (человек), 607, 608 (эльф), 694, 695 (горгулья), 970.</summary>
        public static bool IsGhost(ushort body) => body is 402 or 403 or 607 or 608 or 694 or 695 or 970;

        /// <summary>Тело 0x3DB проходит сквозь двери, как призрак (ignoreDoors в Engines/Pathing/Movement.cs).</summary>
        public static bool PassesDoors(ushort body) => body == 0x03DB;
    }
}
