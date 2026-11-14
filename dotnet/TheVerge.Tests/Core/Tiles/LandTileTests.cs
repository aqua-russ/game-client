using NUnit.Framework;
using TheVerge.Core.Tiles;

namespace TheVerge.Tests.Core.Tiles
{
    /// <summary>
    /// «Пустая» земля, которую сервер не считает опорой: LandTile.Ignored в TileMatrix.cs — номера 2, 0x1DB и 0x1AE–0x1B5.
    /// </summary>
    public sealed class LandTileTests
    {
        [TestCase(2, true)]
        [TestCase(0x1DB, true)]
        [TestCase(0x1AE, true)]
        [TestCase(0x1B5, true)]
        [TestCase(3, false)]
        [TestCase(0x1AD, false)]
        [TestCase(0x1B6, false)]
        [TestCase(0x1DA, false)]
        public void IsIgnored_MatchesServerList(int id, bool ignored)
        {
            Assert.That(new LandTile(id, 0).IsIgnored, Is.EqualTo(ignored));
        }
    }
}
