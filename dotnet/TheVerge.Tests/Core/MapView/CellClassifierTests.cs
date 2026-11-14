using NUnit.Framework;
using TheVerge.Core.MapView;
using TheVerge.Core.Tiles;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>
    /// Цвет клетки по флагам tiledata (PLAN Стадия 2, п. 13): вода, стена, непроходимое, поверхность, остальное. Приоритет —
    /// от самого важного для проходимости; флаги — как TileFlag сервера (Projects/UOContent/Misc/TileData.cs).
    /// </summary>
    public sealed class CellClassifierTests
    {
        [Test]
        public void Wet_BeatsEverything()
        {
            var flags = TileFlags.Wet | TileFlags.Impassable | TileFlags.Wall | TileFlags.Surface;
            Assert.That(CellClassifier.Classify(flags, isLand: true), Is.EqualTo(CellClass.Water));
            Assert.That(CellClassifier.Classify(flags, isLand: false), Is.EqualTo(CellClass.Water));
        }

        [Test]
        public void Wall_BeatsImpassableAndSurface() =>
            Assert.That(CellClassifier.Classify(TileFlags.Wall | TileFlags.Impassable | TileFlags.Surface, false), Is.EqualTo(CellClass.Wall));

        [Test]
        public void Impassable_BeatsSurface() =>
            Assert.That(CellClassifier.Classify(TileFlags.Impassable | TileFlags.Surface, false), Is.EqualTo(CellClass.Blocked));

        [Test]
        public void Surface_IsSurface() =>
            Assert.That(CellClassifier.Classify(TileFlags.Surface, false), Is.EqualTo(CellClass.Surface));

        [Test]
        public void LandWithoutFlags_IsWalkableSurface() =>
            Assert.That(CellClassifier.Classify(TileFlags.None, isLand: true), Is.EqualTo(CellClass.Surface));

        [Test]
        public void StaticWithoutFlags_IsOther() =>
            Assert.That(CellClassifier.Classify(TileFlags.Background | TileFlags.Foliage, isLand: false), Is.EqualTo(CellClass.Other));

        [Test]
        public void ImpassableLand_IsBlocked() =>
            Assert.That(CellClassifier.Classify(TileFlags.Impassable, isLand: true), Is.EqualTo(CellClass.Blocked));
    }
}
