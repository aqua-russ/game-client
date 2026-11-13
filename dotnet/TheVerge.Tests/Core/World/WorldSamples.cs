using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.World
{
    /// <summary>Готовые серийники и описания объектов для тестов мира.</summary>
    internal static class WorldSamples
    {
        public static readonly Serial Player = new Serial(0x0000003D);
        public static readonly Serial Orc = new Serial(0x00000100);
        public static readonly Serial Backpack = new Serial(0x40000010);
        public static readonly Serial Box = new Serial(0x40000020);
        public static readonly Serial Pouch = new Serial(0x40000021);
        public static readonly Serial Apple = new Serial(0x40000022);
        public static readonly Serial Shirt = new Serial(0x40000030);
        public static readonly Serial Hat = new Serial(0x40000031);

        public static MobileInfo Mob(short x, short y, sbyte z = 0, byte notoriety = 1) =>
            new MobileInfo(0x0190, new Point3D(x, y, z), Direction.South, 0x83EA, 0x00, notoriety);

        public static ItemInfo Thing(ushort graphic = 0x0E75, short x = 0, short y = 0, sbyte z = 0) =>
            new ItemInfo(graphic, 1, new Point3D(x, y, z), 0, 0, 0);

        public static PlayerState PlayerAt(short x, short y) =>
            new PlayerState(Player, 0x0190, new Point3D(x, y, 0), Direction.North, new MapSize(7168, 4096));
    }
}
