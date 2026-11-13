using System;
using System.Buffers.Binary;
using NUnit.Framework;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;
using TheVerge.Data.Tiles;

namespace TheVerge.Tests.Data.Tiles
{
    /// <summary>
    /// Tiledata (PLAN Стадия 2, п. 2): земля и предметы в обоих форматах, поле в поле как у сервера (TileData.cs, Load).
    /// Файлы собраны в раскладке клиента UO (<see cref="TileDataFileBuilder"/>); ожидания — то, что из них прочтёт
    /// сервер, в том числе его особенность нового формата: перед записью земли №0 сервер заголовок не читает (он
    /// читает его перед №1), поэтому №0 у него сдвинута на 4 байта. Клиент обязан читать так же — иначе разойдётся
    /// с сервером в проходимости.
    /// </summary>
    public sealed class TileDataReaderTests
    {
        private TempDataFolder _folder = null!;

        [SetUp]
        public void SetUp() => _folder = new TempDataFolder();

        [TearDown]
        public void TearDown() => _folder.Dispose();

        [TestCase(false, 0x8000, 1)]
        [TestCase(false, 0x4000, 2)]
        [TestCase(true, 0x10000, 3)]
        public void EveryRecord_AsServerReadsIt(bool flags64, int items, int seed)
        {
            var file = new TileDataFileBuilder(flags64, items, seed);
            var table = Load(file);

            Assert.That(table.ItemCount, Is.EqualTo(items));
            for (var id = flags64 ? 1 : 0; id < 0x4000; id++)
            {
                var land = table.Land(id);
                Assert.That((ulong)land.Flags, Is.EqualTo(file.LandFlags[id]), "земля " + id);
                Assert.That(land.Name, Is.EqualTo(TileDataFileBuilder.Decode(file.LandNames[id])), "земля " + id);
            }

            for (var id = 0; id < items; id++)
            {
                AssertItem(table.Item(id), file, id);
            }
        }

        [Test]
        public void New64_Land0_ShiftedLikeServer()
        {
            var file = new TileDataFileBuilder(true, 0x10000, 4);
            var bytes = file.Build();
            var table = Load(file);

            // Сервер читает флаги №0 с начала файла (вместе с заголовком), имя — с 10-го байта.
            var land = table.Land(0);
            Assert.That((ulong)land.Flags, Is.EqualTo(BinaryPrimitives.ReadUInt64LittleEndian(bytes)));
            Assert.That(land.Name, Is.EqualTo(TileDataFileBuilder.Decode(bytes.AsSpan(10, 20).ToArray())));
        }

        [Test]
        public void ItemsBeyondFile_Empty()
        {
            var table = Load(new TileDataFileBuilder(false, 0x8000, 5));

            foreach (var id in new[] { 0x8000, 0xABCD, 0xFFFF })
            {
                var item = table.Item(id);
                Assert.That(item.Flags, Is.EqualTo(TileFlags.None));
                Assert.That(item.Name, Is.Empty);
                Assert.That(item.Height, Is.Zero);
            }
        }

        [Test]
        public void Ids_MaskedLikeServer()
        {
            var table = Load(new TileDataFileBuilder(true, 0x10000, 6));

            Assert.That(table.Land(0x4000 + 17).Name, Is.EqualTo(table.Land(17).Name));
            Assert.That(table.Land(-1).Name, Is.EqualTo(table.Land(0x3FFF).Name));
            Assert.That(table.Item(0x10000 + 33).Name, Is.EqualTo(table.Item(33).Name));
        }

        [Test]
        public void Names_AsciiUntilZero_NonAsciiAsQuestionMark()
        {
            var file = new TileDataFileBuilder(false, 0x8000, 7);
            file.ItemNames[10] = Name(0x41, 0xE9, 0x42);
            file.ItemNames[11] = Full('Z');
            file.ItemNames[12] = Name(0x43, 0x00, 0x44);

            var table = Load(file);

            Assert.That(table.Item(10).Name, Is.EqualTo("A?B"));
            Assert.That(table.Item(11).Name, Is.EqualTo(new string('Z', 20)));
            Assert.That(table.Item(12).Name, Is.EqualTo("C"));
        }

        [Test]
        public void CalcHeight_HalfForBridge()
        {
            var file = new TileDataFileBuilder(false, 0x8000, 8);
            file.ItemFlags[20] = (ulong)TileFlags.Bridge;
            file.ItemBytes[20][12] = 9;
            file.ItemFlags[21] = (ulong)TileFlags.Surface;
            file.ItemBytes[21][12] = 9;

            var table = Load(file);

            Assert.That(table.Item(20).CalcHeight, Is.EqualTo(4));
            Assert.That(table.Item(21).CalcHeight, Is.EqualTo(9));
            Assert.That(table.Item(20).Has(TileFlags.Bridge), Is.True);
        }

        [Test]
        public void ShortFile_ErrorNamesFile()
        {
            var bytes = new TileDataFileBuilder(false, 0x8000, 9).Build();
            var path = _folder.Bytes("tiledata.mul", bytes);
            var format = TileDataFormat.ForLength(path, bytes.Length);
            _folder.Bytes("tiledata.mul", bytes.AsSpan(0, bytes.Length - 1).ToArray());

            var table = TileDataReader.Load(format, out var error);

            Assert.That(table, Is.Null);
            Assert.That(error, Does.Contain("tiledata.mul"));
        }

        private ITileData Load(TileDataFileBuilder file)
        {
            var bytes = file.Build();
            var path = _folder.Bytes("tiledata.mul", bytes);
            var table = TileDataReader.Load(TileDataFormat.ForLength(path, bytes.Length), out var error);
            Assert.That(error, Is.Empty);
            return table!;
        }

        private static void AssertItem(ItemTileData item, TileDataFileBuilder file, int id)
        {
            var raw = file.ItemBytes[id];
            var message = "предмет " + id;
            Assert.That((ulong)item.Flags, Is.EqualTo(file.ItemFlags[id]), message);
            Assert.That(item.Weight, Is.EqualTo(raw[0]), message);
            Assert.That(item.Quality, Is.EqualTo(raw[1]), message);
            Assert.That(item.Animation, Is.EqualTo(raw[2] | (raw[3] << 8)), message);
            Assert.That(item.Quantity, Is.EqualTo(raw[5]), message);
            Assert.That(item.Value, Is.EqualTo(raw[11]), message);
            Assert.That(item.Height, Is.EqualTo(raw[12]), message);
            Assert.That(item.Name, Is.EqualTo(TileDataFileBuilder.Decode(file.ItemNames[id])), message);
        }

        private static byte[] Name(params byte[] start)
        {
            var name = new byte[TileDataFileBuilder.NameLength];
            start.CopyTo(name, 0);
            return name;
        }

        private static byte[] Full(char c)
        {
            var name = new byte[TileDataFileBuilder.NameLength];
            Array.Fill(name, (byte)c);
            return name;
        }
    }
}
