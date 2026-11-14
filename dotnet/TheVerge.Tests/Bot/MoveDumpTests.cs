using System.Text.Json;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действие movedump:&lt;проверок&gt;,&lt;seed&gt;,&lt;папка&gt; и сверка физики движения с дампом сервера [DumpMove.
    /// Кейс сервера — 12 чисел: [x, y, z, dir, player, gm, ghost, shove] по паре ok/z на режим
    /// (Custom/TheVerge/Parity/MoveDump.cs; shove — игрок с полной выносливостью, Mobile.CheckShove).
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
            Assert.That(MoveDumpRequest.TryParse("200000,1,/tmp/p", out _), Is.True);
            Assert.That(MoveDumpRequest.TryParse("200001,1,/tmp/p", out _), Is.False);
            Assert.That(MoveDumpRequest.TryParse("100,1,", out _), Is.False);
            Assert.That(MoveDumpRequest.TryParse("abc,1,/tmp/p", out _), Is.False);
            Assert.That(BotAction.Parse("movedump:bad", out _), Is.Null);
        }

        [Test]
        public void Compare_AllModesMatch_ReturnsZeroMismatches()
        {
            var check = CreateFlatCheck();
            using var doc = JsonDocument.Parse(
                "{\"map\":0,\"seed\":1,\"cases\":[[10,10,0,0,1,0,1,0,1,0,1,0],[11,10,0,2,0,0,0,0,0,0,0,0]]}");

            var report = MoveDumpComparison.Compare(doc.RootElement, check, 0);

            Assert.That(report.DifferenceCount, Is.EqualTo(0));
            Assert.That(report.Differences, Is.Empty);
        }

        [Test]
        public void Compare_WhenClientDiffers_ReportsMismatchWithModeAndCoords()
        {
            var check = CreateFlatCheck();
            using var doc = JsonDocument.Parse(
                "{\"map\":0,\"seed\":1,\"cases\":[[10,10,0,0,0,0,1,0,1,0,1,0]]}");

            var report = MoveDumpComparison.Compare(doc.RootElement, check, 0);

            Assert.That(report.DifferenceCount, Is.EqualTo(1));
            Assert.That(report.Differences[0], Does.Contain("(10, 10, 0)").And.Contain("North").And.Contain("player"));
        }

        [Test]
        public void Compare_ShoveMode_PlayerWithFullStaminaPassesThroughMobile()
        {
            var check = CreateCheckWithRatEastOf(10, 10, out uint playerSerial);
            using var match = JsonDocument.Parse(
                "{\"map\":0,\"seed\":1,\"cases\":[[10,10,0,2,0,0,1,0,1,0,1,0]]}");
            using var mismatch = JsonDocument.Parse(
                "{\"map\":0,\"seed\":1,\"cases\":[[10,10,0,2,0,0,1,0,1,0,0,0]]}");

            var same = MoveDumpComparison.Compare(match.RootElement, check, playerSerial);
            var differs = MoveDumpComparison.Compare(mismatch.RootElement, check, playerSerial);

            Assert.That(same.DifferenceCount, Is.EqualTo(0));
            Assert.That(differs.DifferenceCount, Is.EqualTo(1));
            Assert.That(differs.Differences[0], Does.Contain("[shove]"));
        }

        [Test]
        public void Compare_OldDumpWithoutShoveMode_AsksToRebuildServer()
        {
            var check = CreateFlatCheck();
            using var doc = JsonDocument.Parse(
                "{\"map\":0,\"seed\":1,\"cases\":[[10,10,0,0,1,0,1,0,1,0],[11,10,0,2,0,0,0,0,0,0]]}");

            var report = MoveDumpComparison.Compare(doc.RootElement, check, 0);

            Assert.That(report.DifferenceCount, Is.EqualTo(1));
            Assert.That(report.Differences[0], Does.Contain("12").And.Contain("пересоберите сервер"));
        }

        private static MovementCheck CreateCheckWithRatEastOf(short x, short y, out uint playerSerial)
        {
            var map = new FakeMap(32, 32, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            var entities = new EntityStore();
            entities.ChangeMap(0);
            playerSerial = 0x00000010;
            entities.UpdateMobile(new Serial(playerSerial), new MobileInfo(0x0190, new Point3D(x, y, 0), Direction.East, 0, 0, 1));
            entities.UpdateMobile(new Serial(0x00000020), new MobileInfo(0x00EE, new Point3D((short)(x + 1), y, 0), Direction.West, 0, 0, 1));
            return new MovementCheck(map, tiles, new FakeMultis(), entities);
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
