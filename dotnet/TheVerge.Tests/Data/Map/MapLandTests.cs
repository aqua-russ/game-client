using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using TheVerge.Data.Files;
using TheVerge.Data.Map;
using TheVerge.Tests.Data.Files;

namespace TheVerge.Tests.Data.Map
{
    /// <summary>
    /// Земля карты (PLAN Стадия 2, п. 3): клетка → номер земли и Z из MUL и UOP так же, как у сервера (TileMatrix.cs:
    /// ReadLandBlock, FindOffset, ConvertToMapEntries; UOPFiles.cs). Файл не читается целиком — только нужные блоки,
    /// недавние держатся в кэше. Клетка вне карты — отказ. Тестовая карта — 64×32 клетки (8×4 блока).
    /// </summary>
    public sealed class MapLandTests
    {
        private static readonly MapDefinition TestMap = new MapDefinition(0, 0, "Test", 64, 32);
        private TempDataFolder _folder = null!;

        [SetUp]
        public void SetUp() => _folder = new TempDataFolder();

        [TearDown]
        public void TearDown() => _folder.Dispose();

        [Test]
        public void Mul_EveryCellAsWritten()
        {
            var file = new MapFileBuilder(TestMap, 1);
            using (var map = OpenMul(file))
            {
                AssertEveryCell(map, file);
            }
        }

        [TestCase(3, 2)]
        [TestCase(5, 100)]
        [TestCase(32, 1)]
        public void Uop_EveryCellAsWritten(int blocksPerChunk, int entriesPerTable)
        {
            var file = new MapFileBuilder(TestMap, 2);
            using (var map = OpenUop(file.Uop(blocksPerChunk, entriesPerTable)))
            {
                AssertEveryCell(map, file);
            }
        }

        [Test]
        public void Uop_ChunksOrderedByFirstInt_LikeServer()
        {
            // Сервер упорядочивает куски не по номеру в имени, а по первым 4 байтам данных куска (у клиента UO — номер
            // первого блока): TileMatrix.ConvertToMapEntries. Имена здесь перевёрнуты, а данные идут по порядку.
            var file = new MapFileBuilder(TestMap, 3);
            using (var map = OpenUop(file.Uop(4, 3, chunk => 7 - chunk)))
            {
                AssertEveryCell(map, file);
            }
        }

        [Test]
        public void Uop_BlocksNotInFile_EmptyLand()
        {
            // У сервера блок за последним куском даёт пустую землю (FindOffset → −1 → «пустой блок»), а не ошибку.
            var file = new MapFileBuilder(TestMap, 4);
            var builder = new MapFileBuilder(new MapDefinition(0, 0, "Test", 64, 16), 4);
            Array.Copy(file.Mul, builder.Mul, builder.Mul.Length);
            using (var map = OpenUop(builder.Uop(4, 10)))
            {
                Assert.That(map.TryGetLand(1, 2, out var inside), Is.True);
                Assert.That(inside.Id, Is.EqualTo(file.Id(1, 2)));
                Assert.That(map.TryGetLand(63, 31, out var beyond), Is.True);
                Assert.That(beyond.Id, Is.Zero);
                Assert.That(beyond.Z, Is.Zero);
            }
        }

        [Test]
        public void LandId_SignedShortLikeServer()
        {
            var file = new MapFileBuilder(TestMap, 5);
            file.Set(9, 9, unchecked((short)0xFFFF), -128);
            using (var map = OpenMul(file))
            {
                map.TryGetLand(9, 9, out var tile);
                Assert.That(tile.Id, Is.EqualTo(-1));
                Assert.That(tile.Z, Is.EqualTo(-128));
            }
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(64, 0)]
        [TestCase(0, 32)]
        [TestCase(int.MinValue, int.MaxValue)]
        public void OutsideMap_Refused(int x, int y)
        {
            using (var map = OpenMul(new MapFileBuilder(TestMap, 6)))
            {
                Assert.That(map.TryGetLand(x, y, out var tile), Is.False);
                Assert.That(tile.Id, Is.Zero);
            }
        }

        [Test]
        public void ReadsOnlyNeededBlocks_AndCachesThem()
        {
            var file = new MapFileBuilder(TestMap, 7);
            var stream = new CountingStream(new MemoryStream(file.Mul));
            using (var map = new MapData(TestMap, new LandBlocks(TestMap, stream, LandBlockLocator.Mul(), 16), NoStatics()))
            {
                map.TryGetLand(10, 10, out _);
                Assert.That(stream.BytesRead, Is.LessThanOrEqualTo(MapFileBuilder.BlockSize));
                var afterFirst = stream.BytesRead;

                for (var x = 8; x < 16; x++)
                {
                    map.TryGetLand(x, 15, out _);
                }

                Assert.That(stream.BytesRead, Is.EqualTo(afterFirst), "тот же блок — из кэша");
                map.TryGetLand(40, 20, out _);
                Assert.That(stream.BytesRead, Is.LessThanOrEqualTo(2 * MapFileBuilder.BlockSize));
            }
        }

        [Test]
        public void Cache_EvictsLeastRecentlyUsed()
        {
            var file = new MapFileBuilder(TestMap, 8);
            var stream = new CountingStream(new MemoryStream(file.Mul));
            using (var map = new MapData(TestMap, new LandBlocks(TestMap, stream, LandBlockLocator.Mul(), 2), NoStatics()))
            {
                map.TryGetLand(0, 0, out _);
                map.TryGetLand(8, 0, out _);
                map.TryGetLand(0, 0, out _);
                map.TryGetLand(16, 0, out _);
                var read = stream.BytesRead;

                map.TryGetLand(0, 0, out _);
                Assert.That(stream.BytesRead, Is.EqualTo(read), "(0, 0) брали недавно — он в кэше");
                map.TryGetLand(8, 0, out var tile);
                Assert.That(stream.BytesRead, Is.GreaterThan(read), "(8, 0) вытеснен — прочитан заново");
                Assert.That(tile.Id, Is.EqualTo(file.Id(8, 0)));
            }
        }

        [Test]
        public void CachedLookup_NoAllocation_UnderOneMillisecond()
        {
            var file = new MapFileBuilder(TestMap, 9);
            using (var map = OpenMul(file))
            {
                map.TryGetLand(5, 5, out _);
                const int lookups = 100000;
                var watch = new Stopwatch();
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                watch.Start();
                for (var i = 0; i < lookups; i++)
                {
                    map.TryGetLand(i & 7, (i >> 3) & 7, out _);
                }

                watch.Stop();
                Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.Zero);
                Assert.That(watch.Elapsed.TotalMilliseconds / lookups, Is.LessThan(1.0));
            }
        }

        [Test]
        public void BrokenUopIndex_ErrorNotException()
        {
            var bytes = new MapFileBuilder(TestMap, 10).Uop(4, 10);
            BitConverter.GetBytes((long)bytes.Length + 1000).CopyTo(bytes, 12);
            var path = _folder.Bytes("map0LegacyMUL.uop", bytes);
            var files = new MapFiles(TestMap, MapFileKind.Uop, path, _folder.Sized("staidx0.mul", 0), _folder.Sized("statics0.mul", 0));

            var map = MapData.Open(files, 16, out var error);

            Assert.That(map, Is.Null);
            Assert.That(error, Does.Contain("map0LegacyMUL.uop"));
        }

        [Test]
        public void UopTableLoop_ErrorNotHang()
        {
            var builder = new UopFileBuilder(5, 10);
            builder.Add("build/map0legacymul/00000000.dat", new byte[196]);
            var bytes = builder.Build();
            var table = BitConverter.ToInt64(bytes, 12);
            BitConverter.GetBytes(table).CopyTo(bytes, (int)table + 4); // следующая таблица — она же
            var path = _folder.Bytes("map0LegacyMUL.uop", bytes);
            var files = new MapFiles(TestMap, MapFileKind.Uop, path, _folder.Sized("staidx0.mul", 0), _folder.Sized("statics0.mul", 0));

            var map = MapData.Open(files, 16, out var error);

            Assert.That(map, Is.Null);
            Assert.That(error, Does.Contain("map0LegacyMUL.uop"));
        }

        // Индекс из нулей — у каждого блока длина 0, статики нет.
        private static StaticBlocks NoStatics() =>
            new StaticBlocks(TestMap, new MemoryStream(new byte[TestMap.BlockCount * 12]), new MemoryStream(), 16);

        private MapData OpenMul(MapFileBuilder file) => Open(MapFileKind.Mul, _folder.Bytes("map0.mul", file.Mul));

        private MapData OpenUop(byte[] uop) => Open(MapFileKind.Uop, _folder.Bytes("map0LegacyMUL.uop", uop));

        private MapData Open(MapFileKind kind, string path)
        {
            var files = new MapFiles(TestMap, kind, path, _folder.Sized("staidx0.mul", TestMap.BlockCount * 12), _folder.Sized("statics0.mul", 0));
            var map = MapData.Open(files, 16, out var error);
            Assert.That(error, Is.Empty);
            return map!;
        }

        private static void AssertEveryCell(MapData map, MapFileBuilder file)
        {
            Assert.That(map.Width, Is.EqualTo(64));
            Assert.That(map.Height, Is.EqualTo(32));
            for (var x = 0; x < 64; x++)
            {
                for (var y = 0; y < 32; y++)
                {
                    Assert.That(map.TryGetLand(x, y, out var tile), Is.True);
                    Assert.That(tile.Id, Is.EqualTo(file.Id(x, y)), "клетка " + x + ", " + y);
                    Assert.That(tile.Z, Is.EqualTo(file.Z(x, y)), "клетка " + x + ", " + y);
                }
            }
        }
    }
}
