using System;
using System.IO;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Проверка маршрута и шагов из записанного лога пакетов (<c>--check-walk</c>, PLAN Стадия 3, п. 12 и критерий
    /// совместимости с ClassicUO): каждый принятый сервером шаг 0x02 должен приниматься и <see cref="Core.Movement.MovementCheck"/>
    /// с той же позицией и Z (включая лестницы и 0x20), отказы 0x21 и измеренная задержка подтверждения печатаются честно.
    /// </summary>
    public sealed class WalkLogVerifierTests
    {
        private const int Wall = 0x0002;
        private const int Stair = 0x071E;

        [Test]
        public void ValidWalkAcrossStairsAndLag_ZeroMismatches_ReportsPositionsAndLatency()
        {
            var (map, tiles) = CreateTestMap();
            // Лестница в (102, 100) с Z=2, поверхность наверху (103, 100) с Z=4.
            map.AddStatic(102, 100, new StaticTile(Stair, 0, 0));
            map.AddStatic(103, 100, new StaticTile(Stair, 2, 0));
            var log = BuildLog(
                LoginConfirm(0x00000001, 100, 100, 0, Direction.East),
                LoginComplete(),
                MoveReq(1000, Direction.East | Direction.Running, 0),
                MoveAck(1200, 0),
                MoveReq(1200, Direction.East | Direction.Running, 1),
                MoveAck(1410, 1),
                MoveReq(1400, Direction.East, 2),
                MoveAck(1620, 2),
                MobileUpdate(1620, 0x00000001, 103, 100, 4, Direction.East));

            var output = new StringWriter();
            int code = WalkLogVerifier.Run(new StringReader(log), map, tiles, null, "test.tsv", output);

            Assert.That(code, Is.EqualTo(0), output.ToString());
            var text = output.ToString();
            Assert.That(text, Does.Contain("шагов 3"));
            Assert.That(text, Does.Contain("расхождений с картой 0"));
            Assert.That(text, Does.Contain("итог (103, 100, 4)"));
            Assert.That(text, Does.Contain("подтверждение шага: мин 200 мс"));
        }

        [Test]
        public void AcceptedStepIntoBlockedWall_ReportsMismatch_ReturnsNonZero()
        {
            var (map, tiles) = CreateTestMap();
            map.AddStatic(101, 100, new StaticTile(Wall, 0, 0));
            var log = BuildLog(
                LoginConfirm(0x00000001, 100, 100, 0, Direction.East),
                LoginComplete(),
                MoveReq(1000, Direction.East, 0),
                MoveAck(1020, 0));

            var output = new StringWriter();
            int code = WalkLogVerifier.Run(new StringReader(log), map, tiles, null, "wall.tsv", output);

            Assert.That(code, Is.Not.EqualTo(0));
            Assert.That(output.ToString(), Does.Contain("ОШИБКА"));
            Assert.That(output.ToString(), Does.Contain("не пустил"));
        }

        [Test]
        public void RejectAndResync_CountedAndSynced()
        {
            var (map, tiles) = CreateTestMap();
            var log = BuildLog(
                LoginConfirm(0x00000001, 100, 100, 0, Direction.East),
                LoginComplete(),
                MoveReq(1000, Direction.East, 0),
                MoveReject(1050, 0, 100, 100, 0, Direction.East),
                MoveReq(1400, Direction.East, 0),
                MoveAck(1450, 0));

            var output = new StringWriter();
            int code = WalkLogVerifier.Run(new StringReader(log), map, tiles, null, "reject.tsv", output);

            Assert.That(code, Is.EqualTo(0), output.ToString());
            Assert.That(output.ToString(), Does.Contain("отказов сервера (0x21) 1"));
            Assert.That(output.ToString(), Does.Contain("итог (101, 100, 0)"));
        }

        [Test]
        public void ConsecutiveSetupMobileUpdatesBeforeWalking_NotFlaggedAsMismatch()
        {
            var (map, tiles) = CreateTestMap();
            var log = BuildLog(
                LoginConfirm(0x00000001, 100, 100, 0, Direction.East),
                LoginComplete(),
                MobileUpdate(200, 0x00000001, 119, 100, 0, Direction.East),
                MobileUpdate(205, 0x00000001, 120, 100, 0, Direction.East),
                MoveReq(1000, Direction.East, 0),
                MoveAck(1020, 0));

            var output = new StringWriter();
            int code = WalkLogVerifier.Run(new StringReader(log), map, tiles, null, "setup.tsv", output);

            Assert.That(code, Is.EqualTo(0), output.ToString());
            Assert.That(output.ToString(), Does.Contain("итог (121, 100, 0)"));
        }

        [Test]
        public void GoldenWalk20_ParsesAndMatchesFlatTerrain()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Golden", "02-walk-20.tsv");
            var (map, tiles) = CreateTestMap(landZ: 0);
            using var reader = File.OpenText(path);
            var output = new StringWriter();

            int code = WalkLogVerifier.Run(reader, map, tiles, null, "02-walk-20.tsv", output);

            Assert.That(code, Is.EqualTo(0), output.ToString());
            Assert.That(output.ToString(), Does.Contain("шагов 27"));
            Assert.That(output.ToString(), Does.Contain("итог (5445, 1126, 0)"));
        }

        private static (FakeMap Map, ITileData Tiles) CreateTestMap(sbyte landZ = 0)
        {
            var map = new FakeMap(7168, 4096, new LandTile(3, landZ));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            tiles.SetItem(Wall, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            tiles.SetItem(Stair, TileFlags.Surface | TileFlags.Bridge, 4, "stair");
            return (map, tiles);
        }

        private static string BuildLog(params string[] lines) =>
            PacketLogFormat.VersionLine + "\n" + string.Join("\n", lines) + "\n";

        private static string LoginConfirm(uint serial, short x, short y, sbyte z, Direction dir)
        {
            var bytes = new byte[37];
            bytes[0] = 0x1B;
            WriteU32(bytes, 1, serial);
            WriteU16(bytes, 9, 0x0190);
            WriteU16(bytes, 11, (ushort)x);
            WriteU16(bytes, 13, (ushort)y);
            WriteU16(bytes, 15, (ushort)(short)z);
            bytes[17] = (byte)dir;
            WriteU16(bytes, 27, 7168);
            WriteU16(bytes, 29, 4096);
            return Row(0, PacketDirection.ServerToClient, 0x1B, "LoginConfirm", bytes);
        }

        private static string LoginComplete() =>
            Row(10, PacketDirection.ServerToClient, 0x55, "LoginComplete", new byte[] { 0x55 });

        private static string MoveReq(long ms, Direction dir, byte seq) =>
            Row(ms, PacketDirection.ClientToServer, 0x02, "MoveRequest", new byte[] { 0x02, (byte)dir, seq, 0, 0, 0, 0 });

        private static string MoveAck(long ms, byte seq) =>
            Row(ms, PacketDirection.ServerToClient, 0x22, "MoveAck", new byte[] { 0x22, seq, 3 });

        private static string MoveReject(long ms, byte seq, short x, short y, sbyte z, Direction dir)
        {
            var bytes = new byte[8];
            bytes[0] = 0x21;
            bytes[1] = seq;
            WriteU16(bytes, 2, (ushort)x);
            WriteU16(bytes, 4, (ushort)y);
            bytes[6] = (byte)dir;
            bytes[7] = (byte)z;
            return Row(ms, PacketDirection.ServerToClient, 0x21, "MoveReject", bytes);
        }

        private static string MobileUpdate(long ms, uint serial, short x, short y, sbyte z, Direction dir)
        {
            var bytes = new byte[19];
            bytes[0] = 0x20;
            WriteU32(bytes, 1, serial);
            WriteU16(bytes, 5, 0x0190);
            WriteU16(bytes, 11, (ushort)x);
            WriteU16(bytes, 13, (ushort)y);
            bytes[17] = (byte)dir;
            bytes[18] = (byte)z;
            return Row(ms, PacketDirection.ServerToClient, 0x20, "MobileUpdate", bytes);
        }

        private static string Row(long ms, PacketDirection dir, byte id, string name, byte[] bytes)
        {
            string arrow = dir == PacketDirection.ClientToServer ? "C>S" : "S>C";
            string hex = BitConverter.ToString(bytes).Replace("-", " ");
            return $"{ms}\t{arrow}\t0x{id:X2}\t-\t{name}\t{bytes.Length}\t-\t{hex}";
        }

        private static void WriteU16(byte[] dst, int offset, ushort value)
        {
            dst[offset] = (byte)(value >> 8);
            dst[offset + 1] = (byte)value;
        }

        private static void WriteU32(byte[] dst, int offset, uint value)
        {
            dst[offset] = (byte)(value >> 24);
            dst[offset + 1] = (byte)(value >> 16);
            dst[offset + 2] = (byte)(value >> 8);
            dst[offset + 3] = (byte)value;
        }
    }
}