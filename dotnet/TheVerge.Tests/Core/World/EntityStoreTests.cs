using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using static TheVerge.Tests.Core.World.WorldSamples;

namespace TheVerge.Tests.Core.World
{
    /// <summary>
    /// Реестр объектов мира по серийнику (PLAN Стадия 2, п. 7). Диапазоны серийников — как у сервера
    /// (Projects/Server/World/World.cs; тип Serial). Мобы и предметы приходят пакетами 0x78, 0x77, 0x20, 0xF3, 0x2E;
    /// сервер шлёт объект заново при каждом изменении, поэтому клиент обновляет тот же объект на месте.
    /// </summary>
    public sealed class EntityStoreTests
    {
        private EntityStore _store = null!;

        [SetUp]
        public void SetUp() => _store = new EntityStore();

        [Test]
        public void UpdateMobile_New_CreatedWithFields()
        {
            var result = _store.UpdateMobile(Orc, Mob(100, 200, 5, notoriety: 6));

            var orc = _store.FindMobile(Orc);
            Assert.That(result, Is.EqualTo(WorldResult.Applied));
            Assert.That(orc, Is.Not.Null);
            Assert.That(orc!.Body, Is.EqualTo((ushort)0x0190));
            Assert.That(orc.Position, Is.EqualTo(new Point3D(100, 200, 5)));
            Assert.That(orc.Direction, Is.EqualTo(Direction.South));
            Assert.That(orc.Hue, Is.EqualTo((ushort)0x83EA));
            Assert.That(orc.Notoriety, Is.EqualTo((byte)6));
            Assert.That(_store.MobileCount, Is.EqualTo(1));
        }

        [Test]
        public void UpdateMobile_Existing_SameObjectUpdated()
        {
            _store.UpdateMobile(Orc, Mob(100, 200));
            var first = _store.FindMobile(Orc);

            _store.UpdateMobile(Orc, Mob(101, 201));

            Assert.That(_store.FindMobile(Orc), Is.SameAs(first));
            Assert.That(first!.Position, Is.EqualTo(new Point3D(101, 201, 0)));
            Assert.That(_store.MobileCount, Is.EqualTo(1));
        }

        [Test]
        public void UpdateMobile_NotorietyZero_KeepsKnownNotoriety()
        {
            // 0x20 (свой персонаж) и 0xF3 «злости» не несут: прежнее значение остаётся.
            _store.UpdateMobile(Orc, Mob(100, 200, notoriety: 6));

            _store.UpdateMobile(Orc, Mob(101, 200, notoriety: MobileInfo.NotorietyUnknown));

            Assert.That(_store.FindMobile(Orc)!.Notoriety, Is.EqualTo((byte)6));
        }

        [Test]
        public void UpdateExisting_DoesNotAllocate()
        {
            // Установившийся режим (PLAN Стадия 2, критерии): объекты ходят по уже знакомым местам. Первый заход в новый
            // блок карты 8×8 заводит его список в индексе (2.8) — поэтому путь сначала проходится один раз.
            _store.UpdateMobile(Orc, Mob(100, 200));
            _store.PlaceInWorld(Apple, Thing(x: 100, y: 200));
            MoveAlongPath();

            var before = GC.GetAllocatedBytesForCurrentThread();
            MoveAlongPath();

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private void MoveAlongPath()
        {
            for (short i = 0; i < 1000; i++)
            {
                _store.UpdateMobile(Orc, Mob(100, i));
                _store.MoveMobile(Orc, Mob(101, i));
                _store.PlaceInWorld(Apple, Thing(x: 100, y: i));
            }
        }

        [TestCase(0x00000000u)]
        [TestCase(0x40000000u)]
        [TestCase(0x80000000u)]
        public void UpdateMobile_NotMobileSerial_Rejected(uint value)
        {
            var result = _store.UpdateMobile(new Serial(value), Mob(1, 1));

            Assert.That(result, Is.EqualTo(WorldResult.InvalidSerial));
            Assert.That(_store.MobileCount, Is.EqualTo(0));
            Assert.That(_store.ItemCount, Is.EqualTo(0));
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [TestCase(0x00000000u)]
        [TestCase(0x00000100u)]
        [TestCase(0x80000000u)]
        public void PlaceInWorld_NotItemSerial_Rejected(uint value)
        {
            var result = _store.PlaceInWorld(new Serial(value), Thing());

            Assert.That(result, Is.EqualTo(WorldResult.InvalidSerial));
            Assert.That(_store.MobileCount + _store.ItemCount, Is.EqualTo(0));
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void PlaceInWorld_New_ItemOnGround()
        {
            var info = new ItemInfo(0x09D0, 3, new Point3D(10, 20, 30), 0x0481, 0x20, 2);

            _store.PlaceInWorld(Apple, info);

            var apple = _store.FindItem(Apple)!;
            Assert.That(apple.Place, Is.EqualTo(ItemPlace.World));
            Assert.That(apple.Graphic, Is.EqualTo((ushort)0x09D0));
            Assert.That(apple.Amount, Is.EqualTo((ushort)3));
            Assert.That(apple.Position, Is.EqualTo(new Point3D(10, 20, 30)));
            Assert.That(apple.Hue, Is.EqualTo((ushort)0x0481));
            Assert.That(apple.Flags, Is.EqualTo((byte)0x20));
            Assert.That(apple.Light, Is.EqualTo((byte)2));
            Assert.That(apple.IsMulti, Is.False);
            Assert.That(apple.Container, Is.Null);
            Assert.That(apple.Owner, Is.Null);
        }

        [Test]
        public void MobilesAndItems_EnumerateAll()
        {
            _store.UpdateMobile(Orc, Mob(1, 1));
            _store.PlaceInWorld(Box, Thing());
            _store.PlaceInContainer(Apple, Thing(), Box, 0);

            Assert.That(_store.Mobiles.Select(m => m.Serial), Is.EqualTo(new[] { Orc }));
            Assert.That(_store.Items.Select(i => i.Serial), Is.EquivalentTo(new[] { Box, Apple }));
        }

        [Test]
        public void MoveMobile_Unknown_CreatesStubAndCounts()
        {
            var result = _store.MoveMobile(Orc, Mob(5, 6));

            Assert.That(result, Is.EqualTo(WorldResult.Applied));
            Assert.That(_store.FindMobile(Orc)!.Position, Is.EqualTo(new Point3D(5, 6, 0)));
            Assert.That(_store.Metrics.UnknownMobileMoves, Is.EqualTo(1));
        }

        [Test]
        public void MoveMobile_Known_NotCounted()
        {
            _store.UpdateMobile(Orc, Mob(5, 6));

            _store.MoveMobile(Orc, Mob(6, 6));

            Assert.That(_store.Metrics.UnknownMobileMoves, Is.EqualTo(0));
        }

        [Test]
        public void Remove_Unknown_IgnoredAndCounted()
        {
            _store.UpdateMobile(Orc, Mob(5, 6));

            var removed = _store.Remove(new Serial(0x40001234));

            Assert.That(removed, Is.False);
            Assert.That(_store.MobileCount, Is.EqualTo(1));
            Assert.That(_store.Metrics.UnknownRemoves, Is.EqualTo(1));
        }

        [Test]
        public void Remove_Known_Gone()
        {
            _store.UpdateMobile(Orc, Mob(5, 6));
            _store.PlaceInWorld(Apple, Thing());

            Assert.That(_store.Remove(Orc), Is.True);
            Assert.That(_store.Remove(Apple), Is.True);
            Assert.That(_store.FindMobile(Orc), Is.Null);
            Assert.That(_store.FindItem(Apple), Is.Null);
            Assert.That(_store.MobileCount + _store.ItemCount, Is.EqualTo(0));
        }

        [Test]
        public void Reset_StartsNewWorldWithPlayer()
        {
            _store.UpdateMobile(Orc, Mob(5, 6));
            _store.PlaceInWorld(Apple, Thing());

            _store.Reset(PlayerAt(633, 858));

            Assert.That(_store.PlayerSerial, Is.EqualTo(Player));
            Assert.That(_store.Player, Is.Not.Null);
            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(633, 858, 0)));
            Assert.That(_store.Player.Body, Is.EqualTo((ushort)0x0190));
            Assert.That(_store.MobileCount, Is.EqualTo(1));
            Assert.That(_store.ItemCount, Is.EqualTo(0));
        }

        [Test]
        public void Remove_Player_Ignored()
        {
            // Сервер своего персонажа клиенту не удаляет (0x1D шлётся только другим — Mobile.SendRemovePacket).
            _store.Reset(PlayerAt(633, 858));

            Assert.That(_store.Remove(Player), Is.False);
            Assert.That(_store.Player, Is.Not.Null);
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void Clear_KeepPlayer_OnlyPlayerAndHisThingsLeft()
        {
            _store.Reset(PlayerAt(633, 858));
            _store.Equip(Backpack, Thing(0x0E75), Player, Layer.Backpack);
            _store.PlaceInContainer(Apple, Thing(0x09D0), Backpack, 0);
            _store.UpdateMobile(Orc, Mob(634, 858));
            _store.PlaceInWorld(Box, Thing());

            _store.Clear(keepPlayer: true);

            Assert.That(_store.FindMobile(Orc), Is.Null);
            Assert.That(_store.FindItem(Box), Is.Null);
            Assert.That(_store.Player, Is.Not.Null);
            Assert.That(_store.FindItem(Backpack)!.Owner, Is.SameAs(_store.Player));
            Assert.That(_store.FindItem(Apple)!.Container, Is.SameAs(_store.FindItem(Backpack)));
            Assert.That(_store.MobileCount, Is.EqualTo(1));
            Assert.That(_store.ItemCount, Is.EqualTo(2));
        }

        [Test]
        public void Events_RaisedForEveryChange()
        {
            var log = new List<string>();
            _store.MobileChanged += m => log.Add("mobile " + m.Serial);
            _store.ItemChanged += i => log.Add("item " + i.Serial);
            _store.Removed += s => log.Add("removed " + s);
            _store.Cleared += () => log.Add("cleared");

            _store.UpdateMobile(Orc, Mob(1, 1));
            _store.PlaceInWorld(Apple, Thing());
            _store.Remove(Apple);
            _store.Clear(keepPlayer: false);

            Assert.That(log, Is.EqualTo(new[]
            {
                "mobile " + Orc, "item " + Apple, "removed " + Apple, "cleared"
            }));
        }
    }
}
