using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.MapView;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>
    /// Выбор клетки лучом (PLAN Стадия 2, п. 13: «клик по клетке»). Луч — в клетках UO: x на восток, y на юг, Z в единицах
    /// сервера. Побеждает ближайший к началу луча блок; клетка блока и есть выбранная клетка.
    /// </summary>
    public sealed class CellPickerTests
    {
        private readonly List<SceneBlock> _blocks = new List<SceneBlock>();

        [SetUp]
        public void SetUp() => _blocks.Clear();

        private static SceneBlock Block(SceneKind kind, int x, int y, int zBottom, int zTop) =>
            new SceneBlock(kind, x, y, zBottom, zTop, CellClass.Other);

        private static PickRay Down(double x, double y, double z) => new PickRay(x, y, z, 0, 0, -1);

        [Test]
        public void RayFromAbove_HitsCellUnderIt()
        {
            _blocks.Add(Block(SceneKind.Land, 100, 100, 0, 5));

            var hit = CellPicker.TryPick(_blocks, Down(100.5, 100.5, 100), out var result);

            Assert.That(hit, Is.True);
            Assert.That((result.X, result.Y, result.Kind), Is.EqualTo((100, 100, SceneKind.Land)));
            Assert.That(result.Distance, Is.EqualTo(95).Within(1e-9));
        }

        [Test]
        public void RayOutsideAllCells_Misses()
        {
            _blocks.Add(Block(SceneKind.Land, 100, 100, 0, 5));

            Assert.That(CellPicker.TryPick(_blocks, Down(105.5, 100.5, 100), out _), Is.False);
        }

        [Test]
        public void NoBlocks_Misses()
        {
            Assert.That(CellPicker.TryPick(_blocks, Down(100.5, 100.5, 100), out _), Is.False);
        }

        [Test]
        public void StackedBlocks_UpperOneWins()
        {
            _blocks.Add(Block(SceneKind.Land, 100, 100, 0, 5));
            _blocks.Add(Block(SceneKind.Static, 100, 100, 10, 30));

            CellPicker.TryPick(_blocks, Down(100.5, 100.5, 100), out var result);

            Assert.That(result.Kind, Is.EqualTo(SceneKind.Static));
            Assert.That(result.Distance, Is.EqualTo(70).Within(1e-9));
        }

        [Test]
        public void SideRay_NearestCellWins()
        {
            _blocks.Add(Block(SceneKind.Land, 103, 100, 0, 5));
            _blocks.Add(Block(SceneKind.Land, 101, 100, 0, 5));

            CellPicker.TryPick(_blocks, new PickRay(99.5, 100.5, 2, 1, 0, 0), out var result);

            Assert.That(result.X, Is.EqualTo(101));
            Assert.That(result.Distance, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void BlocksBehindTheOrigin_AreNotHit()
        {
            _blocks.Add(Block(SceneKind.Land, 101, 100, 0, 5));

            Assert.That(CellPicker.TryPick(_blocks, new PickRay(105.5, 100.5, 2, 1, 0, 0), out _), Is.False);
        }

        [Test]
        public void OriginInsideBlock_HitsItAtDistanceZero()
        {
            _blocks.Add(Block(SceneKind.Static, 100, 100, 0, 30));

            CellPicker.TryPick(_blocks, new PickRay(100.5, 100.5, 10, 0, 1, 0), out var result);

            Assert.That(result.Distance, Is.Zero);
        }

        [Test]
        public void RayParallelToAxisOutsideSlab_Misses()
        {
            _blocks.Add(Block(SceneKind.Land, 101, 100, 0, 5));

            Assert.That(CellPicker.TryPick(_blocks, new PickRay(99.5, 105.5, 2, 1, 0, 0), out _), Is.False);
        }

        [Test]
        public void ZeroDirection_Misses()
        {
            _blocks.Add(Block(SceneKind.Land, 100, 100, 0, 5));

            Assert.That(CellPicker.TryPick(_blocks, new PickRay(100.5, 100.5, 2, 0, 0, 0), out _), Is.False);
        }

        [Test]
        public void SlantedRay_PassesOverNearCellAndLandsOnFarOne()
        {
            _blocks.Add(Block(SceneKind.Land, 100, 101, 0, 5));
            _blocks.Add(Block(SceneKind.Land, 100, 100, 0, 5));

            // Камера к югу и высоко, смотрит на север и вниз: над ближней клеткой (y = 101) луч выше блока.
            CellPicker.TryPick(_blocks, new PickRay(100.5, 110.5, 100, 0, -10, -95), out var result);

            Assert.That((result.X, result.Y), Is.EqualTo((100, 100)));
        }

        [Test]
        public void MobileStandingOnGround_IsPickedBeforeTheGround()
        {
            _blocks.Add(Block(SceneKind.Land, 100, 100, 0, 5));
            _blocks.Add(Block(SceneKind.Mobile, 100, 100, 5, 20));

            CellPicker.TryPick(_blocks, Down(100.5, 100.5, 100), out var result);

            Assert.That(result.Kind, Is.EqualTo(SceneKind.Mobile));
        }

        [Test]
        public void EqualDistance_LaterBlockWins()
        {
            _blocks.Add(Block(SceneKind.Land, 100, 100, 0, 5));
            _blocks.Add(Block(SceneKind.Item, 100, 100, 0, 5));

            CellPicker.TryPick(_blocks, new PickRay(100.5, 100.5, 3, 0, 1, 0), out var result);

            Assert.That(result.Kind, Is.EqualTo(SceneKind.Item));
        }
    }
}
