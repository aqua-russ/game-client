using System.Text.Json;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Movement;
using TheVerge.Core.Tiles;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действие movedump:&lt;проверок&gt;,&lt;seed&gt;,&lt;папка&gt; и сверка физики движения с дампом сервера [DumpMove.
    /// </summary>
    public sealed class MoveDumpTests
    {
        [Test]
        public void TryParse_ValidAndInvalidArgs()
        {
            Assert.That(MoveDumpRequest.TryParse("100000,1337,/tmp/parity", out var request), Is.True);
            Assert.That((request.Count, request.Seed, request.Directory), Is.EqualTo((100000, 1337, "/tmp/parity")));
            Assert.That(request.Command, Is.EqualTo("[DumpMove 100000 1337"));

            var action = BotAction.Parse("movedump:100,42,/tmp/p", out string error);
            Assert.That(error, Is.Empty);
            Assert.That(action!.Kind, Is.EqualTo(BotActionKind.MoveDump));

            Assert.That(MoveDumpRequest.TryParse("0,1,/tmp/p", out _), Is.False);
            Assert.That(MoveDumpRequest.TryParse("100,1,", out _), Is.False);
            Assert.That(MoveDumpRequest.TryParse("abc,1,/tmp/p", out _), Is.False);
            Assert.That(BotAction.Parse("movedump:bad", out _), Is.Null);
        }

        [Test]
        public void Compare_AllModesMatch_ReturnsZeroMismatches()
        {
            var check = CreateFlatCheck();
            using var doc = JsonDocument.Parse(
                "{\"map\":0,\"seed\":1,\"cases\":[[10,10,0,0,1,0,1,0,1,0],[11,10,0,2,0,0,0,0,0,0]]}");

            var report = MoveDumpComparison.Compare(doc.RootElement, check, 0);

            Assert.That(report.DifferenceCount, Is.EqualTo(0));
            Assert.That(report.Differences, Is.Empty);
        }

        [Test]
        public void Compare_WhenClientDiffers_ReportsMismatchWithModeAndCoords()
        {
            var check = CreateFlatCheck();
            using var doc = JsonDocument.Parse(
                "{\"map\":0,\"seed\":1,\"cases\":[[10,10,0,0,0,0,1,0,1,0]]}");

            var report = MoveDumpComparison.Compare(doc.RootElement, check, 0);

            Assert.That(report.DifferenceCount, Is.EqualTo(1));
            Assert.That(report.Differences[0], Does.Contain("(10, 10, 0)").And.Contain("North").And.Contain("player"));
        }

        private static MovementCheck CreateFlatCheck()
        {
            var map = new FakeMap(32, 32, new LandTile(3, 0));
            map.SetLand(12, 10, new LandTile(4, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            tiles.SetLand(4, TileFlags.Impassable, "rock");
            return new MovementCheck(map, tiles);
        }
    }
}
