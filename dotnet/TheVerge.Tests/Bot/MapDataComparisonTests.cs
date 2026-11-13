using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Multis;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;
using TheVerge.Data.Map;
using TheVerge.Data.Multis;
using TheVerge.Data.Tiles;
using TheVerge.Tests.Data;
using TheVerge.Tests.Data.Map;
using TheVerge.Tests.Data.Tiles;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Сверка читателей клиента с дампом сервера (PLAN Стадия 2, п. 11б). Формат дампа — Custom/TheVerge/Parity/
    /// MapDataDump.cs сервера: cells (x, y, land, z, statics [номер, z, цвет]), staticCells, land [флаги, имя], items
    /// [флаги, вес, качество, анимация, количество, значение, высота, имя], multis (id, границы, parts [номер, x, y, z,
    /// флаги]). Дамп здесь собран из тех же исходных значений, что тестовые файлы; расхождение называет клетку или номер
    /// и поле.
    /// </summary>
    public sealed class MapDataComparisonTests
    {
        private static readonly MapDefinition TestMap = new MapDefinition(0, 0, "Test", 16, 16);
        private TempDataFolder _folder = null!;
        private TileDataFileBuilder _tiles = null!;
        private MapFileBuilder _land = null!;
        private StaticsFileBuilder _statics = null!;
        private Dictionary<int, MultiComponents> _multis = null!;
        private BotWorldData _data = null!;

        [SetUp]
        public void SetUp()
        {
            _folder = new TempDataFolder();
            _tiles = new TileDataFileBuilder(false, 0x4000, 1);
            _land = new MapFileBuilder(TestMap, 2);
            _statics = new StaticsFileBuilder(TestMap, 3);
            _statics.EnsureRecords(0, 3);
            _multis = new Dictionary<int, MultiComponents>
            {
                [5] = MultiComponents.From(new[] { new MultiComponent(0x64, 0, 0, 0, 0), new MultiComponent(0x65, -2, 3, 1, TileFlags.Background) }),
            };
            _data = Data();
        }

        [TearDown]
        public void TearDown()
        {
            _data.Dispose();
            _folder.Dispose();
        }

        [Test]
        public void SameData_NoDifferences()
        {
            var report = Compare(Dump(withStaticCells: true));

            Assert.That(report.Differences, Is.Empty, string.Join("\n", report.Differences));
            var summary = string.Join("\n", report.Summary);
            Assert.That(summary, Does.Contain("клеток 256"));
            Assert.That(summary, Does.Contain("земли 16384").And.Contain("предметов 65536"));
            Assert.That(summary, Does.Contain("домов и кораблей 1"));
        }

        [Test]
        public void LandDiffers_NamesCellAndField()
        {
            var dump = Dump();
            var cell = Cell(dump, 3, 4);
            cell["land"] = cell["land"]!.GetValue<int>() + 1;

            var report = Compare(dump);

            Assert.That(report.DifferenceCount, Is.EqualTo(1));
            Assert.That(report.Differences[0], Does.Contain("клетка (3, 4)").And.Contain("номер земли"));
        }

        [Test]
        public void StaticHueDiffers_NamesCell()
        {
            var dump = Dump();
            var (x, y) = CellWithStatics();
            var tile = (JsonArray)Cell(dump, x, y)["statics"]![0]!;
            tile[2] = tile[2]!.GetValue<int>() ^ 1;

            var report = Compare(dump);

            Assert.That(report.Differences.Single(), Does.Contain("клетка (" + x + ", " + y + ")").And.Contain("цвет"));
        }

        [Test]
        public void ExtraStaticOnServer_NamesCount()
        {
            var dump = Dump();
            ((JsonArray)Cell(dump, 0, 0)["statics"]!).Add(new JsonArray(1, 2, 3));

            Assert.That(Compare(dump).Differences.Single(), Does.Contain("клетка (0, 0)").And.Contain("статики"));
        }

        [Test]
        public void ItemAndLandFields_NamedById()
        {
            var dump = Dump();
            var item = (JsonArray)dump["items"]![0x123]!;
            item[1] = item[1]!.GetValue<int>() ^ 1;
            ((JsonArray)dump["land"]![0x45]!)[1] = "other";

            var differences = Compare(dump).Differences;

            Assert.That(differences, Has.Some.Contains("предмет 0x0123").And.Some.Contains("вес"));
            Assert.That(differences, Has.Some.Contains("земля 0x0045").And.Some.Contains("имя"));
        }

        [Test]
        public void MultiDiffers_OrMissing_NamesId()
        {
            var dump = Dump();
            var multi = (JsonObject)dump["multis"]![0]!;
            multi["width"] = 99;
            ((JsonArray)dump["multis"]!).Add(new JsonObject
            {
                ["id"] = 9, ["minX"] = 0, ["minY"] = 0, ["maxX"] = 0, ["maxY"] = 0, ["centerX"] = 0, ["centerY"] = 0,
                ["width"] = 1, ["height"] = 1, ["parts"] = new JsonArray(),
            });

            var differences = Compare(dump).Differences;

            Assert.That(differences, Has.Some.Contains("дом или корабль № 5").And.Some.Contains("ширина"));
            Assert.That(differences, Has.Some.Contains("дом или корабль № 9"));
        }

        [Test]
        public void ClientHasMultiServerNot_Named()
        {
            var dump = Dump();
            ((JsonArray)dump["multis"]!).Clear();

            Assert.That(Compare(dump).Differences.Single(), Does.Contain("дом или корабль № 5"));
        }

        [Test]
        public void StaticCellsCount_Compared()
        {
            var dump = Dump(withStaticCells: true);
            ((JsonArray)dump["staticCells"]!).RemoveAt(0);

            Assert.That(Compare(dump).Differences, Has.Some.Contains("клеток со статикой"));
        }

        [Test]
        public void MapSizeDiffers_Named()
        {
            var dump = Dump();
            dump["width"] = 32;

            Assert.That(Compare(dump).Differences.Single(), Does.Contain("размер карты"));
        }

        [Test]
        public void ManyDifferences_FirstTwentyKept_AllCounted()
        {
            var dump = Dump();
            foreach (var node in (JsonArray)dump["items"]!)
            {
                var item = (JsonArray)node!;
                item[7] = "x" + item[7]!.GetValue<string>();
            }

            var report = Compare(dump);

            Assert.That(report.DifferenceCount, Is.EqualTo(0x10000));
            Assert.That(report.Differences.Count, Is.EqualTo(MapDumpReport.MaxListed));
        }

        private MapDumpReport Compare(JsonObject dump)
        {
            using (var document = JsonDocument.Parse(dump.ToJsonString()))
            {
                return MapDataComparison.Compare(document.RootElement, _data);
            }
        }

        private BotWorldData Data()
        {
            var tileBytes = _tiles.Build();
            var format = TileDataFormat.ForLength(_folder.Bytes("tiledata.mul", tileBytes), tileBytes.Length);
            var tiles = TileDataReader.ReadFile(format, out _)!;
            _statics.Build(out var index, out var data);
            var map = new MapData(TestMap, new LandBlocks(TestMap, new MemoryStream(_land.Mul), LandBlockLocator.Mul(), 16),
                new StaticBlocks(TestMap, new MemoryStream(index), new MemoryStream(data), 16));
            var layout = new DataLayout(_folder.Path, new MapFiles(TestMap, MapFileKind.Mul, "map0.mul", "staidx0.mul", "statics0.mul"),
                format, MultiFiles.Mul("multi.idx", "multi.mul", true));
            return new BotWorldData(layout, tiles, map, new MultiTable(_multis));
        }

        // Дамп «как у сервера», собранный из исходных значений тестовых файлов (а не из читателей клиента).
        private JsonObject Dump(bool withStaticCells = false)
        {
            var cells = new JsonArray();
            var staticCells = new JsonArray();
            for (var x = 0; x < TestMap.Width; x++)
            {
                for (var y = 0; y < TestMap.Height; y++)
                {
                    cells.Add(CellNode(x, y));
                    if (_statics.Cell(x, y).Count > 0)
                    {
                        staticCells.Add(CellNode(x, y));
                    }
                }
            }

            var dump = new JsonObject
            {
                ["player"] = "0x00000001", ["map"] = 0, ["width"] = 16, ["height"] = 16, ["seed"] = 1,
                ["cells"] = cells, ["land"] = LandNodes(), ["items"] = ItemNodes(), ["multis"] = MultiNodes(),
            };
            if (withStaticCells)
            {
                dump["staticCells"] = staticCells;
            }

            return dump;
        }

        private JsonObject CellNode(int x, int y)
        {
            var statics = new JsonArray();
            foreach (var record in _statics.Cell(x, y))
            {
                statics.Add(new JsonArray(record[0] | (record[1] << 8), (int)(sbyte)record[4], (int)(short)(record[5] | (record[6] << 8))));
            }

            return new JsonObject { ["x"] = x, ["y"] = y, ["land"] = (int)_land.Id(x, y), ["z"] = (int)_land.Z(x, y), ["statics"] = statics };
        }

        private JsonArray LandNodes()
        {
            var land = new JsonArray();
            for (var i = 0; i < 0x4000; i++)
            {
                land.Add(new JsonArray(_tiles.LandFlags[i], TileDataFileBuilder.Decode(_tiles.LandNames[i])));
            }

            return land;
        }

        private JsonArray ItemNodes()
        {
            var items = new JsonArray();
            for (var i = 0; i < 0x10000; i++)
            {
                if (i >= _tiles.ItemCount)
                {
                    items.Add(new JsonArray(0UL, 0, 0, 0, 0, 0, 0, ""));
                    continue;
                }

                var raw = _tiles.ItemBytes[i];
                items.Add(new JsonArray(_tiles.ItemFlags[i], (int)raw[0], (int)raw[1], raw[2] | (raw[3] << 8), (int)raw[5], (int)raw[11], (int)raw[12],
                    TileDataFileBuilder.Decode(_tiles.ItemNames[i])));
            }

            return items;
        }

        private JsonArray MultiNodes() => new JsonArray(new JsonObject
        {
            ["id"] = 5, ["minX"] = -2, ["minY"] = 0, ["maxX"] = 0, ["maxY"] = 3, ["centerX"] = 2, ["centerY"] = 0,
            ["width"] = 3, ["height"] = 4,
            ["parts"] = new JsonArray(new JsonArray(0x64, 0, 0, 0, 0UL), new JsonArray(0x65, -2, 3, 1, (ulong)TileFlags.Background)),
        });

        private static JsonObject Cell(JsonObject dump, int x, int y) =>
            (JsonObject)((JsonArray)dump["cells"]!).Single(c => c!["x"]!.GetValue<int>() == x && c["y"]!.GetValue<int>() == y)!;

        private (int, int) CellWithStatics()
        {
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    if (_statics.Cell(x, y).Count > 0)
                    {
                        return (x, y);
                    }
                }
            }

            throw new System.InvalidOperationException("в блоке 0 нет статики");
        }
    }
}
