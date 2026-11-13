using System;
using System.IO;
using NUnit.Framework;
using TheVerge.Data.Files;
using TheVerge.Data.Map;

namespace TheVerge.Tests.Data.Files
{
    /// <summary>
    /// Пути и формат файлов данных (PLAN Стадия 2, п. 1). Формат выбирается так же, как у сервера:
    /// карта — map{N}.mul, а если его нет — map{N}LegacyMUL.uop (TileMatrix.cs, конструктор); tiledata — по длине файла
    /// (TileData.cs, Load: от 3 188 736 байт — флаги 64 бита и 0x10000 предметов, от 1 644 544 — 32 бита и 0x8000,
    /// иначе 32 бита и 0x4000); дома — MultiCollection.uop, а если его нет — multi.idx + multi.mul, запись 16 или 12
    /// байт по настройке maps.enablePostHSMultiComponentFormat (MultiData.cs, Configure). Имена файлов — без учёта
    /// регистра, как сервер ищет их в Linux (Core.FindDataFile).
    /// </summary>
    public sealed class DataLayoutDetectorTests
    {
        // Фелукка 7168×4096 (map-definitions.json сервера): 896×512 блоков по 196 байт карты и 12 байт индекса статики.
        private const long FeluccaMulLength = 896L * 512 * 196;
        private const long FeluccaStaticIndexLength = 896L * 512 * 12;
        private const long TileData64Length = 3188736;
        private const long TileData32Length = 1644544;
        // Самый старый вид: 512 групп земли по (4 + 32×26) и 512 групп предметов по (4 + 32×37) байт.
        private const long TileDataOldestLength = 512L * (4 + 32 * 26) + 512L * (4 + 32 * 37);

        private TempDataFolder _folder = null!;

        [SetUp]
        public void SetUp()
        {
            _folder = new TempDataFolder();
            _folder.Sized("map0.mul", FeluccaMulLength);
            _folder.Sized("staidx0.mul", FeluccaStaticIndexLength);
            _folder.Sized("statics0.mul", 0);
            _folder.Sized("tiledata.mul", TileData32Length);
            _folder.Sized("multi.idx", 0);
            _folder.Sized("multi.mul", 0);
        }

        [TearDown]
        public void TearDown() => _folder.Dispose();

        [Test]
        public void MulFiles_AllDetected()
        {
            var layout = Detect();

            Assert.That(layout.Map.Kind, Is.EqualTo(MapFileKind.Mul));
            Assert.That(layout.Map.MapPath, Is.EqualTo(_folder.File("map0.mul")));
            Assert.That(layout.Map.IndexPath, Is.EqualTo(_folder.File("staidx0.mul")));
            Assert.That(layout.Map.StaticsPath, Is.EqualTo(_folder.File("statics0.mul")));
            Assert.That(layout.TileData.Path, Is.EqualTo(_folder.File("tiledata.mul")));
            Assert.That(layout.Multi.Kind, Is.EqualTo(MultiFileKind.Mul));
            Assert.That(layout.Multi.IndexPath, Is.EqualTo(_folder.File("multi.idx")));
            Assert.That(layout.Multi.DataPath, Is.EqualTo(_folder.File("multi.mul")));
        }

        [Test]
        public void UopMap_WhenNoMulMap()
        {
            _folder.Delete("map0.mul");
            _folder.Bytes("map0LegacyMUL.uop", UopHeader(5));

            var layout = Detect();

            Assert.That(layout.Map.Kind, Is.EqualTo(MapFileKind.Uop));
            Assert.That(layout.Map.MapPath, Is.EqualTo(_folder.File("map0LegacyMUL.uop")));
        }

        [Test]
        public void MulMap_PreferredOverUop_AsServer()
        {
            _folder.Bytes("map0LegacyMUL.uop", UopHeader(5));

            Assert.That(Detect().Map.Kind, Is.EqualTo(MapFileKind.Mul));
        }

        [TestCase(TileData64Length, true, 0x10000)]
        [TestCase(TileData64Length + 100, true, 0x10000)]
        [TestCase(TileData64Length - 1, false, 0x8000)]
        [TestCase(TileData32Length, false, 0x8000)]
        [TestCase(TileData32Length - 1, false, 0x4000)]
        [TestCase(TileDataOldestLength, false, 0x4000)]
        public void TileDataFormat_ByLength(long length, bool flags64, int items)
        {
            _folder.Sized("tiledata.mul", length);

            var format = Detect().TileData;

            Assert.That(format.Is64BitFlags, Is.EqualTo(flags64));
            Assert.That(format.ItemCount, Is.EqualTo(items));
            Assert.That(TileDataFormat.LandCount, Is.EqualTo(0x4000));
        }

        [Test]
        public void TileData_TooShort_Error()
        {
            _folder.Sized("tiledata.mul", TileDataOldestLength - 1);

            Assert.That(DetectError(), Does.Contain("tiledata.mul").And.Contain(TileDataOldestLength.ToString()));
        }

        [Test]
        public void MultiUop_PreferredOverMul()
        {
            _folder.Bytes("MultiCollection.uop", UopHeader(4));

            var multi = Detect().Multi;

            Assert.That(multi.Kind, Is.EqualTo(MultiFileKind.Uop));
            Assert.That(multi.UopPath, Is.EqualTo(_folder.File("MultiCollection.uop")));
        }

        [Test]
        public void MultiMul_RecordSize_FromOption()
        {
            Assert.That(Detect().Multi.MulRecordSize, Is.EqualTo(16));

            var old = DataLayoutDetector.Detect(_folder.Path, MapDefinition.Felucca, new DataOptions { MultiMulPostHsFormat = false });

            Assert.That(old.Layout!.Multi.MulRecordSize, Is.EqualTo(12));
        }

        [Test]
        public void FileNames_CaseInsensitive()
        {
            File.Move(_folder.File("tiledata.mul"), _folder.File("TileData.MUL"));
            File.Move(_folder.File("map0.mul"), _folder.File("MAP0.mul"));

            var layout = Detect();

            Assert.That(layout.TileData.Path, Is.EqualTo(_folder.File("TileData.MUL")));
            Assert.That(layout.Map.MapPath, Is.EqualTo(_folder.File("MAP0.mul")));
        }

        [Test]
        public void MissingFolder_Error()
        {
            var missing = _folder.File("нет-такой");

            var detection = DataLayoutDetector.Detect(missing, MapDefinition.Felucca, new DataOptions());

            Assert.That(detection.IsValid, Is.False);
            Assert.That(detection.Layout, Is.Null);
            Assert.That(detection.Error, Does.Contain(missing));
        }

        [Test]
        public void MissingMap_ErrorNamesBothVariants()
        {
            _folder.Delete("map0.mul");

            Assert.That(DetectError(), Does.Contain("map0.mul").And.Contain("map0LegacyMUL.uop"));
        }

        [TestCase("staidx0.mul")]
        [TestCase("statics0.mul")]
        [TestCase("tiledata.mul")]
        public void MissingFile_ErrorNamesIt(string name)
        {
            _folder.Delete(name);

            Assert.That(DetectError(), Does.Contain(name));
        }

        [TestCase("multi.idx")]
        [TestCase("multi.mul")]
        public void MissingMulti_ErrorNamesFiles(string name)
        {
            _folder.Delete(name);

            Assert.That(DetectError(), Does.Contain(name).And.Contain("MultiCollection.uop"));
        }

        [Test]
        public void ShortMulMap_Error()
        {
            _folder.Sized("map0.mul", FeluccaMulLength - 1);

            Assert.That(DetectError(), Does.Contain("map0.mul").And.Contain(FeluccaMulLength.ToString()));
        }

        [Test]
        public void ShortStaticIndex_Error()
        {
            _folder.Sized("staidx0.mul", FeluccaStaticIndexLength - 12);

            Assert.That(DetectError(), Does.Contain("staidx0.mul").And.Contain(FeluccaStaticIndexLength.ToString()));
        }

        [TestCase(0x12345678u, 5, 0xFD23EC43u)]
        [TestCase(0x50594Du, 4, 0xFD23EC43u)]
        [TestCase(0x50594Du, 5, 0x11111111u)]
        public void UopMap_BadHeader_Error(uint magic, int version, uint signature)
        {
            _folder.Delete("map0.mul");
            _folder.Bytes("map0LegacyMUL.uop", UopHeader(magic, version, signature));

            Assert.That(DetectError(), Does.Contain("map0LegacyMUL.uop"));
        }

        [Test]
        public void UopMap_TooShortForHeader_Error()
        {
            _folder.Delete("map0.mul");
            _folder.Bytes("map0LegacyMUL.uop", new byte[] { 0x4D, 0x59, 0x50 });

            Assert.That(DetectError(), Does.Contain("map0LegacyMUL.uop"));
        }

        [Test]
        public void UopMulti_BadVersion_Error()
        {
            _folder.Bytes("MultiCollection.uop", UopHeader(5));

            Assert.That(DetectError(), Does.Contain("MultiCollection.uop"));
        }

        [Test]
        public void Describe_NamesFilesAndFormats()
        {
            _folder.Delete("map0.mul");
            _folder.Bytes("map0LegacyMUL.uop", UopHeader(5));
            _folder.Sized("tiledata.mul", TileData64Length);
            _folder.Bytes("MultiCollection.uop", UopHeader(4));

            var text = string.Join("\n", Detect().Describe());

            Assert.That(text, Does.Contain(_folder.Path));
            Assert.That(text, Does.Contain("map0LegacyMUL.uop").And.Contain("UOP").And.Contain("7168×4096"));
            Assert.That(text, Does.Contain("staidx0.mul").And.Contain("statics0.mul"));
            Assert.That(text, Does.Contain("tiledata.mul").And.Contain("64").And.Contain("0x10000"));
            Assert.That(text, Does.Contain("MultiCollection.uop"));
        }

        [Test]
        public void Describe_MulMulti_NamesRecordSize()
        {
            var text = string.Join("\n", Detect().Describe());

            Assert.That(text, Does.Contain("multi.idx").And.Contain("multi.mul").And.Contain("16"));
            Assert.That(text, Does.Contain("32").And.Contain("0x8000"));
        }

        private DataLayout Detect()
        {
            var detection = DataLayoutDetector.Detect(_folder.Path, MapDefinition.Felucca, new DataOptions());
            Assert.That(detection.Error, Is.Empty);
            Assert.That(detection.IsValid, Is.True);
            return detection.Layout!;
        }

        private string DetectError()
        {
            var detection = DataLayoutDetector.Detect(_folder.Path, MapDefinition.Felucca, new DataOptions());
            Assert.That(detection.IsValid, Is.False);
            Assert.That(detection.Layout, Is.Null);
            return detection.Error;
        }

        private static byte[] UopHeader(int version) => UopHeader(0x50594D, version, 0xFD23EC43);

        // Заголовок UOP, как его читает сервер (UOPFiles.ReadUOPIndexes): «MYP\0», версия, подпись, адрес первой таблицы.
        private static byte[] UopHeader(uint magic, int version, uint signature)
        {
            var data = new byte[20];
            BitConverter.GetBytes(magic).CopyTo(data, 0);
            BitConverter.GetBytes(version).CopyTo(data, 4);
            BitConverter.GetBytes(signature).CopyTo(data, 8);
            return data;
        }
    }
}
