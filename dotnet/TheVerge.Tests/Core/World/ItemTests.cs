using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.World
{
    /// <summary>
    /// Признак «переносимый» — флаг 0x20 из Item.GetPacketFlags сервера (Movable или ForceShowProperties).
    /// </summary>
    public sealed class ItemTests
    {
        [Test]
        public void IsMovable_ComesFromServerFlag0x20()
        {
            var world = new EntityStore();
            world.ChangeMap(0);
            world.PlaceInWorld(new Serial(0x40000001), new ItemInfo(0x0E40, 1, new Point3D(1, 1, 0), 0, 0x20, 0));
            world.PlaceInWorld(new Serial(0x40000002), new ItemInfo(0x0E40, 1, new Point3D(2, 1, 0), 0, 0x80, 0));

            Assert.That(world.FindItem(new Serial(0x40000001))!.IsMovable, Is.True);
            Assert.That(world.FindItem(new Serial(0x40000002))!.IsMovable, Is.False);
        }
    }
}
