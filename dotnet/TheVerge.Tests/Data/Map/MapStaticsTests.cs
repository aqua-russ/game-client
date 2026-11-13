using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;
using TheVerge.Data.Map;

namespace TheVerge.Tests.Data.Map
{
    /// <summary>
    /// Статика карты (PLAN Стадия 2, п. 4): клетка → список статики (номер, Z, цвет) в том же порядке и с теми же
    /// значениями, что у сервера (TileMatrix.ReadStaticBlock: блок целиком, раскладка по клеткам по X и Y младшими
    /// 3 битами, порядок файла). Пустой блок — пустой список, клетка вне карты — отказ. Тот же кэш блоков, что у земли.
    /// </summary>
    public sealed class MapStaticsTests
    {
        // 56×40 клеток: 7×5 = 35 блоков; в файле они лежат в перемешанном порядке (StaticsFileBuilder).
        private static readonly MapDefinition TestMap = new MapDefinition(0, 0, "Test", 56, 40);
        private TempDataFolder _folder = null!;

        [SetUp]
        public void SetUp() => _folder = new TempDataFolder();

        [TearDown]
        public void TearDown() => _folder.Dispose();

        [Test]
        public void EveryCell_AsServer()
        {
            var file = new StaticsFileBuilder(TestMap, 1);
            var result = new List<StaticTile>();
            using (var map = Open(file))
            {
                for (var x = 0; x < TestMap.Width; x++)
                {
                    for (var y = 0; y < TestMap.Height; y++)
                    {
                        Assert.That(map.TryGetStatics(x, y, result), Is.True);
                        AssertSame(result, file.Cell(x, y), x, y);
                    }
                }
            }
        }

        [Test]
        public void EmptyBlocks_EmptyList()
        {
            var file = new StaticsFileBuilder(TestMap, 2);
            file.Blocks[0].Clear();
            file.Blocks[1].Clear();
            var result = new List<StaticTile> { new StaticTile(1, 2, 3) };
            using (var map = Open(file))
            {
                Assert.That(map.TryGetStatics(3, 3, result), Is.True);
                Assert.That(result, Is.Empty);
                Assert.That(map.TryGetStatics(3, 11, result), Is.True);
                Assert.That(result, Is.Empty);
            }
        }

        [TestCase(-1, 0)]
        [TestCase(0, 40)]
        [TestCase(56, 0)]
        public void OutsideMap_RefusedAndCleared(int x, int y)
        {
            var result = new List<StaticTile> { new StaticTile(1, 2, 3) };
            using (var map = Open(new StaticsFileBuilder(TestMap, 3)))
            {
                Assert.That(map.TryGetStatics(x, y, result), Is.False);
                Assert.That(result, Is.Empty);
            }
        }

        [Test]
        public void SignedZAndHue_LikeServer()
        {
            var file = new StaticsFileBuilder(TestMap, 4);
            file.Blocks[0].Clear();
            file.Blocks[0].Add(new byte[] { 0x34, 0x12, 0x02, 0x03, 0x80, 0xFF, 0xFF });

            using (var map = Open(file))
            {
                var result = new List<StaticTile>();
                map.TryGetStatics(2, 3, result);
                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result[0].Id, Is.EqualTo(0x1234));
                Assert.That(result[0].Z, Is.EqualTo(-128));
                Assert.That(result[0].Hue, Is.EqualTo(-1));
            }
        }

        [Test]
        public void LengthNotMultipleOf7_WholeRecordsOnly()
        {
            var file = new StaticsFileBuilder(TestMap, 5);
            file.EnsureRecords(0, 3);
            file.Build(out var index, out var data);
            var length = BitConverter.ToInt32(index, 4);
            BitConverter.GetBytes(length - 3).CopyTo(index, 4);
            var expected = file.Blocks[0].GetRange(0, file.Blocks[0].Count - 1);
            file.Blocks[0].Clear();
            file.Blocks[0].AddRange(expected);

            using (var map = Open(index, data))
            {
                AssertBlock(map, file, 0);
            }
        }

        [Test]
        public void BlockCutByEndOfFile_WholeRecordsOnly()
        {
            // Сервер в таком случае подставил бы остаток прошлого прочитанного блока (общий буфер); в настоящих файлах
            // так не бывает, клиент берёт только целые записи, дочитанные из файла.
            var file = new StaticsFileBuilder(TestMap, 6);
            var last = file.LastBlockInFile;
            file.EnsureRecords(last, 5);
            file.Build(out var index, out var data);
            Array.Resize(ref data, data.Length - 10);
            file.Blocks[last].RemoveRange(file.Blocks[last].Count - 2, 2);

            using (var map = Open(index, data))
            {
                AssertBlock(map, file, last);
            }
        }

        [Test]
        public void ShortIndex_EmptyBlocks()
        {
            var file = new StaticsFileBuilder(TestMap, 7);
            file.Build(out var index, out var data);
            Array.Resize(ref index, 12 * 10);
            for (var block = 10; block < file.Blocks.Length; block++)
            {
                file.Blocks[block].Clear();
            }

            using (var map = Open(index, data))
            {
                AssertBlock(map, file, 3);
                AssertBlock(map, file, 20);
            }
        }

        [Test]
        public void SameBlock_ReadOnce_NoAllocation()
        {
            var file = new StaticsFileBuilder(TestMap, 8);
            file.Build(out var index, out var data);
            var indexStream = new CountingStream(new MemoryStream(index));
            var dataStream = new CountingStream(new MemoryStream(data));
            var land = new LandBlocks(TestMap, new MemoryStream(new byte[TestMap.BlockCount * 196]), LandBlockLocator.Mul(), 16);
            using (var map = new MapData(TestMap, land, new StaticBlocks(TestMap, indexStream, dataStream, 16)))
            {
                var result = new List<StaticTile>(256);
                map.TryGetStatics(9, 9, result);
                var read = indexStream.BytesRead + dataStream.BytesRead;
                Assert.That(indexStream.BytesRead, Is.LessThanOrEqualTo(12));

                var allocated = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 1000; i++)
                {
                    map.TryGetStatics(8 + (i & 7), 8 + ((i >> 3) & 7), result);
                }

                Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.Zero);
                Assert.That(indexStream.BytesRead + dataStream.BytesRead, Is.EqualTo(read));
            }
        }

        private MapData Open(StaticsFileBuilder file)
        {
            file.Build(out var index, out var data);
            return Open(index, data);
        }

        private MapData Open(byte[] index, byte[] data)
        {
            var files = new MapFiles(TestMap, MapFileKind.Mul, _folder.Sized("map0.mul", TestMap.BlockCount * 196),
                _folder.Bytes("staidx0.mul", index), _folder.Bytes("statics0.mul", data));
            var map = MapData.Open(files, 16, out var error);
            Assert.That(error, Is.Empty);
            return map!;
        }

        private static void AssertBlock(MapData map, StaticsFileBuilder file, int block)
        {
            var bx = block / TestMap.BlockHeight * 8;
            var by = block % TestMap.BlockHeight * 8;
            var result = new List<StaticTile>();
            for (var x = bx; x < bx + 8; x++)
            {
                for (var y = by; y < by + 8; y++)
                {
                    map.TryGetStatics(x, y, result);
                    AssertSame(result, file.Cell(x, y), x, y);
                }
            }
        }

        private static void AssertSame(List<StaticTile> actual, List<byte[]> expected, int x, int y)
        {
            var where = "клетка " + x + ", " + y;
            Assert.That(actual.Count, Is.EqualTo(expected.Count), where);
            for (var i = 0; i < expected.Count; i++)
            {
                var record = expected[i];
                Assert.That(actual[i].Id, Is.EqualTo(record[0] | (record[1] << 8)), where);
                Assert.That(actual[i].Z, Is.EqualTo((sbyte)record[4]), where);
                Assert.That(actual[i].Hue, Is.EqualTo((short)(record[5] | (record[6] << 8))), where);
            }
        }
    }
}
