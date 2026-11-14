using NUnit.Framework;
using TheVerge.Core.Multis;
using TheVerge.Core.Tiles;

namespace TheVerge.Tests.Core.Multis
{
    /// <summary>
    /// Какие части дома занимают клетку и на какой высоте — как MultiComponentList.Tiles сервера (MultiData.cs):
    /// первая часть всегда, остальные — только с ненулевыми флагами; высота части — sbyte (StaticTile.Z, TileMatrix.cs).
    /// </summary>
    public sealed class MultiComponentsTests
    {
        [Test]
        public void IsPlaced_FirstAlways_OthersOnlyWithFlags()
        {
            var multi = MultiComponents.From(new[]
            {
                new MultiComponent(0x0001, 0, 0, 0, TileFlags.None),
                new MultiComponent(0x0002, 1, 0, 0, TileFlags.None),
                new MultiComponent(0x0003, 2, 0, 0, TileFlags.Background),
            });

            Assert.That(multi.IsPlaced(0), Is.True);
            Assert.That(multi.IsPlaced(1), Is.False);
            Assert.That(multi.IsPlaced(2), Is.True);
        }

        [Test]
        public void WorldZ_AddsMultiZ_AndWrapsLikeServerSbyte()
        {
            Assert.That(new MultiComponent(0x0001, 0, 0, 10, TileFlags.Background).WorldZ(5), Is.EqualTo(15));
            Assert.That(new MultiComponent(0x0001, 0, 0, 100, TileFlags.Background).WorldZ(100), Is.EqualTo(-56));
        }
    }
}
