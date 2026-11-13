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
    /// Поиск объектов рядом с клеткой (PLAN Стадия 2, п. 8). Дальность — квадрат, как у сервера
    /// (Projects/Server/Utilities/Utility.cs, InRange: |dx| ≤ r и |dy| ≤ r). В поиск попадают мобы и предметы на земле;
    /// надетое и лежащее в контейнере — нет (у них нет клетки на карте).
    /// </summary>
    public sealed class SpatialIndexTests
    {
        private EntityStore _store = null!;
        private readonly List<Mobile> _mobiles = new List<Mobile>();
        private readonly List<Item> _items = new List<Item>();

        [SetUp]
        public void SetUp() => _store = new EntityStore();

        [Test]
        public void RandomChanges_QueryMatchesBruteForce()
        {
            var seed = Environment.TickCount;
            var random = new Random(seed);
            for (var step = 0; step < 10000; step++)
            {
                RandomChange(random);
                if (step % 10 == 0)
                {
                    var x = (short)random.Next(-20, 220);
                    var y = (short)random.Next(-20, 220);
                    var radius = random.Next(0, 40);
                    AssertMatchesBruteForce(x, y, radius, "seed " + seed + ", шаг " + step);
                }
            }
        }

        [Test]
        public void EquippedAndContained_NotFound()
        {
            _store.UpdateMobile(Orc, Mob(50, 50));
            _store.Equip(Shirt, Thing(), Orc, Layer.Shirt);
            _store.PlaceInWorld(Box, Thing(x: 50, y: 50));
            _store.PlaceInContainer(Apple, Thing(), Box, 0);

            _store.Spatial.QueryItems(50, 50, 5, _items);
            _store.Spatial.QueryMobiles(50, 50, 5, _mobiles);

            Assert.That(_items.Select(i => i.Serial), Is.EqualTo(new[] { Box }));
            Assert.That(_mobiles.Select(m => m.Serial), Is.EqualTo(new[] { Orc }));
        }

        [Test]
        public void ItemTakenFromGround_NoLongerFound()
        {
            _store.PlaceInWorld(Box, Thing());
            _store.PlaceInWorld(Apple, Thing(x: 1, y: 1));

            _store.PlaceInContainer(Apple, Thing(), Box, 0);

            _store.Spatial.QueryItems(1, 1, 0, _items);
            Assert.That(_items, Is.Empty);
        }

        [Test]
        public void RadiusIsSquare_EdgesIncluded()
        {
            _store.PlaceInWorld(new Serial(0x40000001), Thing(x: 118, y: 82));
            _store.PlaceInWorld(new Serial(0x40000002), Thing(x: 82, y: 118));
            _store.PlaceInWorld(new Serial(0x40000003), Thing(x: 119, y: 100));

            _store.Spatial.QueryItems(100, 100, 18, _items);

            Assert.That(_items.Select(i => i.Serial.Value), Is.EquivalentTo(new[] { 0x40000001u, 0x40000002u }));
        }

        [Test]
        public void RadiusZero_OnlySameCell()
        {
            _store.PlaceInWorld(new Serial(0x40000001), Thing(x: 0, y: 0));
            _store.PlaceInWorld(new Serial(0x40000002), Thing(x: 1, y: 0));

            _store.Spatial.QueryItems(0, 0, 0, _items);

            Assert.That(_items.Select(i => i.Serial.Value), Is.EqualTo(new[] { 0x40000001u }));
        }

        [Test]
        public void NegativeAndMapEdges_Work()
        {
            _store.PlaceInWorld(new Serial(0x40000001), Thing(x: -3, y: -1));
            _store.PlaceInWorld(new Serial(0x40000002), Thing(x: 7167, y: 4095));
            _store.UpdateMobile(Orc, Mob(short.MaxValue, short.MinValue));

            _store.Spatial.QueryItems(0, 0, 3, _items);
            Assert.That(_items.Count, Is.EqualTo(1));
            _store.Spatial.QueryItems(7167, 4095, 0, _items);
            Assert.That(_items.Count, Is.EqualTo(1));
            _store.Spatial.QueryMobiles(short.MaxValue, short.MinValue, 2, _mobiles);
            Assert.That(_mobiles.Count, Is.EqualTo(1));
            _store.Spatial.QueryItems(0, 0, -1, _items);
            Assert.That(_items, Is.Empty);
        }

        [Test]
        public void HugeRadius_FindsAll()
        {
            _store.PlaceInWorld(new Serial(0x40000001), Thing(x: 0, y: 0));
            _store.PlaceInWorld(new Serial(0x40000002), Thing(x: 7000, y: 4000));

            _store.Spatial.QueryItems(3500, 2000, int.MaxValue, _items);

            Assert.That(_items.Count, Is.EqualTo(2));
        }

        [Test]
        public void Clear_IndexFollows()
        {
            _store.Reset(PlayerAt(10, 10));
            _store.UpdateMobile(Orc, Mob(11, 10));
            _store.PlaceInWorld(Box, Thing(x: 10, y: 10));

            _store.Clear(keepPlayer: true);
            _store.Spatial.QueryMobiles(10, 10, 5, _mobiles);
            _store.Spatial.QueryItems(10, 10, 5, _items);
            Assert.That(_mobiles.Select(m => m.Serial), Is.EqualTo(new[] { Player }));
            Assert.That(_items, Is.Empty);

            _store.Clear(keepPlayer: false);
            _store.Spatial.QueryMobiles(10, 10, 5, _mobiles);
            Assert.That(_mobiles, Is.Empty);
        }

        [Test]
        public void QueryAndMoves_DoNotAllocate()
        {
            for (uint i = 0; i < 50; i++)
            {
                _store.PlaceInWorld(new Serial(0x40000100 + i), Thing(x: (short)i, y: 3));
            }

            _store.UpdateMobile(Orc, Mob(5, 5));
            MoveAndQuery(); // первый проход заводит блоки и места в списках результатов

            var before = GC.GetAllocatedBytesForCurrentThread();
            MoveAndQuery();

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private void MoveAndQuery()
        {
            for (short i = 0; i < 1000; i++)
            {
                _store.UpdateMobile(Orc, Mob((short)(i % 30), 5));
                _store.PlaceInWorld(new Serial(0x40000100), Thing(x: (short)(i % 30), y: 3));
                _store.Spatial.QueryItems(10, 10, 20, _items);
                _store.Spatial.QueryMobiles(10, 10, 20, _mobiles);
            }
        }

        private void RandomChange(Random random)
        {
            var mobile = new Serial((uint)random.Next(1, 40));
            var item = new Serial(0x40000000u + (uint)random.Next(0, 120));
            var x = (short)random.Next(0, 200);
            var y = (short)random.Next(0, 200);
            switch (random.Next(7))
            {
                case 0: _store.UpdateMobile(mobile, Mob(x, y)); break;
                case 1: _store.MoveMobile(mobile, Mob(x, y)); break;
                case 2: _store.PlaceInWorld(item, Thing(x: x, y: y)); break;
                case 3: _store.PlaceInContainer(item, Thing(), new Serial(0x40000000u + (uint)random.Next(0, 120)), 0); break;
                case 4: _store.Equip(item, Thing(), mobile, (Layer)random.Next(1, 0x1E)); break;
                case 5: _store.Remove(random.Next(2) == 0 ? mobile : item); break;
                default: if (random.Next(200) == 0) { _store.Clear(random.Next(2) == 0); } break;
            }
        }

        private void AssertMatchesBruteForce(short x, short y, int radius, string context)
        {
            _store.Spatial.QueryMobiles(x, y, radius, _mobiles);
            _store.Spatial.QueryItems(x, y, radius, _items);

            var mobiles = AllMobiles().Where(m => Near(m.Position, x, y, radius)).Select(m => m.Serial.Value);
            var items = AllItems().Where(i => i.Place == ItemPlace.World && Near(i.Position, x, y, radius)).Select(i => i.Serial.Value);
            Assert.That(_mobiles.Select(m => m.Serial.Value), Is.EquivalentTo(mobiles), "мобы, " + context);
            Assert.That(_items.Select(i => i.Serial.Value), Is.EquivalentTo(items), "предметы, " + context);
        }

        private IEnumerable<Mobile> AllMobiles()
        {
            for (uint i = 1; i < 40; i++)
            {
                var mobile = _store.FindMobile(new Serial(i));
                if (mobile != null)
                {
                    yield return mobile;
                }
            }
        }

        private IEnumerable<Item> AllItems()
        {
            for (uint i = 0; i < 120; i++)
            {
                var item = _store.FindItem(new Serial(0x40000000u + i));
                if (item != null)
                {
                    yield return item;
                }
            }
        }

        private static bool Near(Point3D p, short x, short y, int radius) =>
            Math.Abs(p.X - x) <= radius && Math.Abs(p.Y - y) <= radius;
    }
}
