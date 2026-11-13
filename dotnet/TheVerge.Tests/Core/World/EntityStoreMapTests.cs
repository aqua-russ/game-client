using System;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;
using static TheVerge.Tests.Core.World.WorldSamples;

namespace TheVerge.Tests.Core.World
{
    /// <summary>
    /// Карта и дальность (PLAN Стадия 2, п. 9). Номер карты приходит только в 0xBF 0x08; при входе сервер шлёт его дважды
    /// (IncomingAccountPackets.cs, DoLogin и после 0x55). При настоящей смене карты сервер сам удаляет всё у клиента
    /// (Mobile.Map → ClearScreen) и шлёт заново. Когда свой персонаж уходит, сервер 0x1D за оставшимися позади объектами
    /// не шлёт (Mobile.SetLocation: 0x1D получают только другие, кто перестал видеть его самого), поэтому клиент удаляет
    /// их сам: дальше 18 клеток (Item.GetUpdateRange, Utility.InUpdateRange), multi — дальше 22 (BaseMulti.GetUpdateRange).
    /// </summary>
    public sealed class EntityStoreMapTests
    {
        private EntityStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _store.Reset(PlayerAt(1000, 1000));
        }

        [Test]
        public void ChangeMap_FirstTime_NothingRemoved()
        {
            _store.UpdateMobile(Orc, Mob(1001, 1000));

            _store.ChangeMap(0);

            Assert.That(_store.MapIndex, Is.EqualTo(0));
            Assert.That(_store.FindMobile(Orc), Is.Not.Null);
        }

        [Test]
        public void ChangeMap_SameMap_NothingRemoved()
        {
            _store.ChangeMap(0);
            _store.UpdateMobile(Orc, Mob(1001, 1000));

            _store.ChangeMap(0);

            Assert.That(_store.FindMobile(Orc), Is.Not.Null);
        }

        [Test]
        public void ChangeMap_OtherMap_OnlyPlayerAndHisThingsLeft()
        {
            _store.ChangeMap(0);
            _store.Equip(Backpack, Thing(), Player, Layer.Backpack);
            _store.UpdateMobile(Orc, Mob(1001, 1000));
            _store.PlaceInWorld(Box, Thing(x: 1000, y: 1000));

            _store.ChangeMap(1);

            Assert.That(_store.MapIndex, Is.EqualTo(1));
            Assert.That(_store.FindMobile(Orc), Is.Null);
            Assert.That(_store.FindItem(Box), Is.Null);
            Assert.That(_store.FindItem(Backpack), Is.Not.Null);
        }

        [Test]
        public void Reset_MapUnknownAgain()
        {
            _store.ChangeMap(0);

            _store.Reset(PlayerAt(1, 1));

            Assert.That(_store.MapIndex, Is.EqualTo(EntityStore.UnknownMap));
        }

        [Test]
        public void PlayerMoves_FarObjectsRemoved_MultiKeptToItsRange()
        {
            // Расстояния — от клетки (1000, 1001), куда персонаж шагнёт.
            _store.UpdateMobile(new Serial(0x200), Mob(1018, 1001));
            _store.UpdateMobile(new Serial(0x201), Mob(1019, 1001));
            _store.PlaceInWorld(new Serial(0x40000200), Thing(x: 1000, y: 983));
            _store.PlaceInWorld(new Serial(0x40000201), Thing(x: 1000, y: 982));
            _store.PlaceInWorld(new Serial(0x40000202), new ItemInfo(0x4000, 1, new Point3D(978, 1001, 0), 0, 0, 0, isMulti: true));
            _store.PlaceInWorld(new Serial(0x40000203), new ItemInfo(0x4000, 1, new Point3D(977, 1001, 0), 0, 0, 0, isMulti: true));
            _store.Equip(Shirt, Thing(), new Serial(0x201), Layer.Shirt);

            _store.UpdateMobile(Player, Mob(1000, 1000, 5)); // клетка та же: ничего не удаляется
            Assert.That(_store.MobileCount, Is.EqualTo(3));
            Assert.That(_store.ItemCount, Is.EqualTo(5));

            _store.MovePlayerTo(new Point3D(1000, 1001, 0));

            Assert.That(_store.FindMobile(new Serial(0x200)), Is.Not.Null, "моб на 18 клетках остаётся");
            Assert.That(_store.FindMobile(new Serial(0x201)), Is.Null, "моб на 19 клетках удалён");
            Assert.That(_store.FindItem(Shirt), Is.Null, "вместе с надетым");
            Assert.That(_store.FindItem(new Serial(0x40000200)), Is.Not.Null, "предмет на 18 клетках остаётся");
            Assert.That(_store.FindItem(new Serial(0x40000201)), Is.Null, "предмет на 19 клетках удалён");
            Assert.That(_store.FindItem(new Serial(0x40000202)), Is.Not.Null, "multi на 22 клетках остаётся");
            Assert.That(_store.FindItem(new Serial(0x40000203)), Is.Null, "multi на 23 клетках удалён");
        }

        [Test]
        public void PruneOff_FarObjectsKept()
        {
            _store.PruneOutOfRange = false;
            _store.UpdateMobile(Orc, Mob(1030, 1000));

            _store.MovePlayerTo(new Point3D(999, 1000, 0));

            Assert.That(_store.FindMobile(Orc), Is.Not.Null);
        }

        [Test]
        public void PruneDefault_FromClientLimits()
        {
            Assert.That(new EntityStore().PruneOutOfRange, Is.EqualTo(ClientLimits.RemoveOutOfRange));
            Assert.That(ClientLimits.RemoveOutOfRange, Is.True);
        }

        [Test]
        public void OtherMobileMoves_NothingPruned()
        {
            // Чужой моб, ушедший из виду, сервер удаляет сам (0x1D): клиент от его шага ничего не удаляет.
            _store.UpdateMobile(Orc, Mob(1001, 1000));
            _store.PlaceInWorld(Box, Thing(x: 1000, y: 1000));

            _store.MoveMobile(Orc, Mob(1100, 1000));

            Assert.That(_store.FindItem(Box), Is.Not.Null);
            Assert.That(_store.FindMobile(Orc), Is.Not.Null);
        }

        [Test]
        public void MovePlayerTo_KeepsOtherFields()
        {
            _store.UpdateMobile(Player, new MobileInfo(0x191, new Point3D(1000, 1000, 0), Direction.East, 0x400, 0x02, 1));

            _store.MovePlayerTo(new Point3D(1500, 1600, 7));

            var player = _store.Player!;
            Assert.That(player.Position, Is.EqualTo(new Point3D(1500, 1600, 7)));
            Assert.That(player.Body, Is.EqualTo((ushort)0x191));
            Assert.That(player.Direction, Is.EqualTo(Direction.East));
            Assert.That(player.Hue, Is.EqualTo((ushort)0x400));
            Assert.That(player.Notoriety, Is.EqualTo((byte)1));
        }

        [Test]
        public void PlayerSteps_NoAllocations()
        {
            for (uint i = 0; i < 20; i++)
            {
                _store.UpdateMobile(new Serial(0x300 + i), Mob((short)(1000 + i), 1000));
            }

            Walk();
            var before = GC.GetAllocatedBytesForCurrentThread();
            Walk();

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private void Walk()
        {
            for (var i = 0; i < 200; i++)
            {
                _store.MovePlayerTo(new Point3D((short)(1000 + i % 5), 1000, 0));
            }
        }
    }
}
