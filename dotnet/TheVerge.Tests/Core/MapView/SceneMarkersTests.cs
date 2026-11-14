using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.MapView;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using static TheVerge.Tests.Core.World.WorldSamples;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>
    /// Мобы и предметы на земле как блоки для выбора кликом: в тех же клетках и по высоте, что рисует Presentation
    /// (моб — 15 единиц Z, предмет — 4). Дома — не предметы для клика: их части уже есть в блоках карты.
    /// </summary>
    public sealed class SceneMarkersTests
    {
        private EntityStore _world = null!;
        private readonly List<SceneBlock> _blocks = new List<SceneBlock>();

        [SetUp]
        public void SetUp()
        {
            _world = new EntityStore();
        }

        [Test]
        public void Mobile_BecomesCylinderSizedBlock()
        {
            _world.UpdateMobile(Orc, Mob(100, 101, 7));

            SceneMarkers.Collect(_world, _blocks);

            var mobile = _blocks.Find(b => b.Kind == SceneKind.Mobile);
            Assert.That((mobile.X, mobile.Y, mobile.ZBottom, mobile.ZTop), Is.EqualTo((100, 101, 7, 22)));
        }

        [Test]
        public void GroundItem_BecomesCubeSizedBlock()
        {
            _world.PlaceInWorld(Apple, Thing(0x09D0, 102, 103, 5));

            SceneMarkers.Collect(_world, _blocks);

            var item = _blocks.Find(b => b.Kind == SceneKind.Item);
            Assert.That((item.X, item.Y, item.ZBottom, item.ZTop), Is.EqualTo((102, 103, 5, 9)));
        }

        [Test]
        public void ItemInContainer_IsNotABlock()
        {
            _world.PlaceInWorld(Box, Thing(0x0E75, 100, 100, 0));
            _world.PlaceInContainer(Apple, Thing(0x09D0), Box, 0);

            SceneMarkers.Collect(_world, _blocks);

            Assert.That(_blocks.FindAll(b => b.Kind == SceneKind.Item), Has.Count.EqualTo(1));
        }

        [Test]
        public void HouseOrShip_IsNotABlock()
        {
            _world.PlaceInWorld(Backpack, new ItemInfo(0x10, 1, new Point3D(100, 100, 0), 0, 0, 0, isMulti: true));

            SceneMarkers.Collect(_world, _blocks);

            Assert.That(_blocks.FindAll(b => b.Kind == SceneKind.Item), Is.Empty);
        }

        [Test]
        public void Collect_ClearsPreviousResult()
        {
            _blocks.Add(new SceneBlock(SceneKind.Land, 1, 1, 0, 1, CellClass.Other));

            SceneMarkers.Collect(_world, _blocks);

            Assert.That(_blocks.TrueForAll(b => b.Kind != SceneKind.Land), Is.True);
        }
    }
}
