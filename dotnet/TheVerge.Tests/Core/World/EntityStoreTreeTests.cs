using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using static TheVerge.Tests.Core.World.WorldSamples;

namespace TheVerge.Tests.Core.World
{
    /// <summary>
    /// Связи «контейнер → содержимое» и «моб → надетое по слоям» (PLAN Стадия 2, п. 7). Слои — Layer.cs сервера: на
    /// одном слое у моба один предмет (Mobile.EquipItem проверяет занятость слоя). Удалённый с сервера предмет пропадает
    /// у клиента вместе со всем, что в нём лежит: сервер шлёт 0x1D только на сам контейнер (Item.SendRemovePacket).
    /// </summary>
    public sealed class EntityStoreTreeTests
    {
        private EntityStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _store.Reset(PlayerAt(100, 100));
        }

        [Test]
        public void Equip_ItemOnLayer()
        {
            var result = _store.Equip(Shirt, Thing(0x1517), Player, Layer.Shirt);

            var shirt = _store.FindItem(Shirt)!;
            Assert.That(result, Is.EqualTo(WorldResult.Applied));
            Assert.That(shirt.Place, Is.EqualTo(ItemPlace.Equipped));
            Assert.That(shirt.Layer, Is.EqualTo(Layer.Shirt));
            Assert.That(shirt.Owner, Is.SameAs(_store.Player));
            Assert.That(_store.Player!.GetEquipped(Layer.Shirt), Is.SameAs(shirt));
            Assert.That(_store.Player.EquippedCount, Is.EqualTo(1));
        }

        [Test]
        public void Equip_OccupiedLayer_OldItemRemoved()
        {
            _store.Equip(Shirt, Thing(0x1517), Player, Layer.Shirt);

            _store.Equip(Hat, Thing(0x1718), Player, Layer.Shirt);

            Assert.That(_store.Player!.GetEquipped(Layer.Shirt)!.Serial, Is.EqualTo(Hat));
            Assert.That(_store.FindItem(Shirt), Is.Null);
            Assert.That(_store.Player.EquippedCount, Is.EqualTo(1));
        }

        [TestCase((byte)0x00)]
        [TestCase((byte)0x1E)]
        [TestCase((byte)0xFF)]
        public void Equip_InvalidLayer_Rejected(byte layer)
        {
            var result = _store.Equip(Shirt, Thing(), Player, (Layer)layer);

            Assert.That(result, Is.EqualTo(WorldResult.InvalidLayer));
            Assert.That(_store.FindItem(Shirt), Is.Null);
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void Equip_UnknownMobile_Rejected()
        {
            var result = _store.Equip(Shirt, Thing(), Orc, Layer.Shirt);

            Assert.That(result, Is.EqualTo(WorldResult.UnknownParent));
            Assert.That(_store.FindItem(Shirt), Is.Null);
            Assert.That(_store.FindMobile(Orc), Is.Null);
        }

        [Test]
        public void Equip_ItemSerialAsMobile_Rejected()
        {
            _store.PlaceInWorld(Box, Thing());

            var result = _store.Equip(Shirt, Thing(), Box, Layer.Shirt);

            Assert.That(result, Is.EqualTo(WorldResult.InvalidSerial));
            Assert.That(_store.FindItem(Shirt), Is.Null);
        }

        [Test]
        public void PlaceInContainer_ItemInside()
        {
            _store.PlaceInWorld(Box, Thing(0x0E7D, 101, 100));

            var info = new ItemInfo(0x09D0, 5, new Point3D(44, 65, 0), 0x0021, 0, 0);
            var result = _store.PlaceInContainer(Apple, info, Box, 3);

            var apple = _store.FindItem(Apple)!;
            var box = _store.FindItem(Box)!;
            Assert.That(result, Is.EqualTo(WorldResult.Applied));
            Assert.That(apple.Place, Is.EqualTo(ItemPlace.Container));
            Assert.That(apple.Container, Is.SameAs(box));
            Assert.That(apple.Position, Is.EqualTo(new Point3D(44, 65, 0)));
            Assert.That(apple.GridIndex, Is.EqualTo((byte)3));
            Assert.That(box.Contents, Is.EqualTo(new[] { apple }));
        }

        [Test]
        public void PlaceInContainer_UnknownContainer_StubCreatedAndCounted()
        {
            var result = _store.PlaceInContainer(Apple, Thing(0x09D0), Box, 0);

            var box = _store.FindItem(Box)!;
            Assert.That(result, Is.EqualTo(WorldResult.Applied));
            Assert.That(box.Place, Is.EqualTo(ItemPlace.Unplaced));
            Assert.That(box.Contents.Count, Is.EqualTo(1));
            Assert.That(_store.Metrics.StubContainers, Is.EqualTo(1));
        }

        [Test]
        public void Stub_FilledByLaterPacket()
        {
            _store.PlaceInContainer(Apple, Thing(0x09D0), Box, 0);

            _store.PlaceInWorld(Box, Thing(0x0E7D, 105, 106));

            var box = _store.FindItem(Box)!;
            Assert.That(box.Place, Is.EqualTo(ItemPlace.World));
            Assert.That(box.Graphic, Is.EqualTo((ushort)0x0E7D));
            Assert.That(box.Contents.Count, Is.EqualTo(1));
        }

        [TestCase(0x00000000u)]
        [TestCase(0x0000003Du)]
        [TestCase(0x80000000u)]
        public void PlaceInContainer_ContainerNotItem_Rejected(uint container)
        {
            var result = _store.PlaceInContainer(Apple, Thing(), new Serial(container), 0);

            Assert.That(result, Is.EqualTo(WorldResult.InvalidSerial));
            Assert.That(_store.FindItem(Apple), Is.Null);
        }

        [Test]
        public void Move_BetweenPlaces_LeavesOldPlace()
        {
            _store.PlaceInWorld(Box, Thing());
            _store.PlaceInWorld(Pouch, Thing());
            _store.PlaceInContainer(Apple, Thing(), Box, 0);

            _store.PlaceInContainer(Apple, Thing(), Pouch, 0);
            Assert.That(_store.FindItem(Box)!.Contents, Is.Empty);
            Assert.That(_store.FindItem(Pouch)!.Contents.Count, Is.EqualTo(1));

            _store.Equip(Apple, Thing(), Player, Layer.OneHanded);
            Assert.That(_store.FindItem(Pouch)!.Contents, Is.Empty);
            Assert.That(_store.FindItem(Apple)!.Container, Is.Null);

            _store.PlaceInWorld(Apple, Thing(x: 1, y: 2));
            var apple = _store.FindItem(Apple)!;
            Assert.That(_store.Player!.GetEquipped(Layer.OneHanded), Is.Null);
            Assert.That(apple.Owner, Is.Null);
            Assert.That(apple.Place, Is.EqualTo(ItemPlace.World));
        }

        [Test]
        public void Remove_Container_RemovesContentsAtAnyDepth()
        {
            _store.PlaceInWorld(Box, Thing());
            _store.PlaceInContainer(Pouch, Thing(), Box, 0);
            _store.PlaceInContainer(Apple, Thing(), Pouch, 0);
            var removed = new List<Serial>();
            _store.Removed += removed.Add;

            _store.Remove(Box);

            Assert.That(_store.ItemCount, Is.EqualTo(0));
            Assert.That(removed, Is.EquivalentTo(new[] { Box, Pouch, Apple }));
        }

        [Test]
        public void Remove_Mobile_RemovesEquipmentAndBackpackContents()
        {
            _store.UpdateMobile(Orc, Mob(101, 100));
            _store.Equip(Backpack, Thing(0x0E75), Orc, Layer.Backpack);
            _store.Equip(Shirt, Thing(0x1517), Orc, Layer.Shirt);
            _store.PlaceInContainer(Apple, Thing(), Backpack, 0);

            _store.Remove(Orc);

            Assert.That(_store.FindItem(Backpack), Is.Null);
            Assert.That(_store.FindItem(Shirt), Is.Null);
            Assert.That(_store.FindItem(Apple), Is.Null);
            Assert.That(_store.ItemCount, Is.EqualTo(0));
        }

        [Test]
        public void Remove_ItemInContainer_LeavesContainer()
        {
            _store.PlaceInWorld(Box, Thing());
            _store.PlaceInContainer(Apple, Thing(), Box, 0);

            _store.Remove(Apple);

            Assert.That(_store.FindItem(Box)!.Contents, Is.Empty);
        }

        [Test]
        public void Remove_EquippedItem_FreesLayer()
        {
            _store.Equip(Shirt, Thing(), Player, Layer.Shirt);

            _store.Remove(Shirt);

            Assert.That(_store.Player!.GetEquipped(Layer.Shirt), Is.Null);
            Assert.That(_store.Player.EquippedCount, Is.EqualTo(0));
        }

        [Test]
        public void PlaceInContainer_IntoItself_RejectedWorldUnchanged()
        {
            _store.PlaceInWorld(Box, Thing(x: 7, y: 8));

            var result = _store.PlaceInContainer(Box, Thing(), Box, 0);

            var box = _store.FindItem(Box)!;
            Assert.That(result, Is.EqualTo(WorldResult.Cycle));
            Assert.That(box.Place, Is.EqualTo(ItemPlace.World));
            Assert.That(box.Position, Is.EqualTo(new Point3D(7, 8, 0)));
            Assert.That(box.Contents, Is.Empty);
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void PlaceInContainer_IntoOwnContents_RejectedWorldUnchanged()
        {
            _store.PlaceInWorld(Box, Thing());
            _store.PlaceInContainer(Pouch, Thing(), Box, 0);
            _store.PlaceInContainer(Apple, Thing(), Pouch, 0);

            var result = _store.PlaceInContainer(Box, Thing(), Apple, 0);

            Assert.That(result, Is.EqualTo(WorldResult.Cycle));
            Assert.That(_store.FindItem(Box)!.Place, Is.EqualTo(ItemPlace.World));
            Assert.That(_store.FindItem(Apple)!.Contents, Is.Empty);
            Assert.That(_store.FindItem(Pouch)!.Container!.Serial, Is.EqualTo(Box));
        }

        [Test]
        public void EquipmentUpdate_ItemsNotRepeated_Removed()
        {
            // 0x78 несёт весь видимый список надетого (OutgoingMobilePackets.cs, SendMobileIncoming).
            _store.UpdateMobile(Orc, Mob(101, 100));
            _store.Equip(Shirt, Thing(), Orc, Layer.Shirt);
            _store.Equip(Hat, Thing(), Orc, Layer.Helm);
            _store.Equip(Backpack, Thing(), Orc, Layer.Backpack);
            _store.PlaceInContainer(Apple, Thing(), Backpack, 0);

            _store.BeginEquipment(Orc);
            _store.Equip(Hat, Thing(), Orc, Layer.Helm);
            _store.Equip(Backpack, Thing(), Orc, Layer.Backpack);
            _store.EndEquipment(Orc);

            var orc = _store.FindMobile(Orc)!;
            Assert.That(_store.FindItem(Shirt), Is.Null);
            Assert.That(orc.GetEquipped(Layer.Helm)!.Serial, Is.EqualTo(Hat));
            Assert.That(_store.FindItem(Apple)!.Container!.Serial, Is.EqualTo(Backpack));
            Assert.That(orc.EquippedCount, Is.EqualTo(2));
        }

        [Test]
        public void DeepNesting_RemovedWithoutStackOverflow()
        {
            _store.PlaceInWorld(new Serial(0x40100000), Thing());
            for (uint i = 1; i < 100000; i++)
            {
                _store.PlaceInContainer(new Serial(0x40100000 + i), Thing(), new Serial(0x40100000 + i - 1), 0);
            }

            _store.Remove(new Serial(0x40100000));

            Assert.That(_store.ItemCount, Is.EqualTo(0));
        }
    }
}
