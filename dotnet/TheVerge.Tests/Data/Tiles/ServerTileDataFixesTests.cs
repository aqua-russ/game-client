using NUnit.Framework;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;
using TheVerge.Data.Files;
using TheVerge.Data.Tiles;

namespace TheVerge.Tests.Data.Tiles
{
    /// <summary>
    /// Правки tiledata, которые сервер делает при запуске поверх файла (найдено сверкой tools/check/map.sh): по ним сервер
    /// решает видимость и шаг, поэтому клиент обязан видеть ту же таблицу. Источники в UOContent сервера:
    /// Special Systems/Engines/ItemFixes.cs (0x2A0, 0x3E0, 0x3E1 — NoShoot; 0x34D2 — высота 0), Engines/ConPVP/Games/CTF.cs
    /// (0x7C9–0x7D0 — NoShoot), Items/Misc/LOSBlocker.cs (0x21A2 — флаги Wall | NoShoot, высота 20),
    /// Items/Skill Items/Magical/Misc/PotionKeg.cs (0x1940 — высота 4). Остальное — как в файле.
    /// </summary>
    public sealed class ServerTileDataFixesTests
    {
        private TempDataFolder _folder = null!;
        private TileDataFileBuilder _file = null!;
        private ITileData _raw = null!;
        private ITileData _table = null!;

        [SetUp]
        public void SetUp()
        {
            _folder = new TempDataFolder();
            _file = new TileDataFileBuilder(true, 0x10000, 11);
            foreach (var id in new[] { 0x2A0, 0x3E0, 0x3E1, 0x7C9, 0x7CC, 0x7D0 })
            {
                _file.ItemFlags[id] &= ~(ulong)TileFlags.NoShoot; // иначе добавление флага не видно
            }

            _file.ItemBytes[0x1940][12] = 9;
            _file.ItemBytes[0x34D2][12] = 9;
            _file.ItemBytes[0x21A2][12] = 9;
            _file.ItemFlags[0x2A0] = (ulong)TileFlags.Wall;
            _file.ItemFlags[0x21A2] = (ulong)(TileFlags.Surface | TileFlags.Wet);
            var bytes = _file.Build();
            var format = TileDataFormat.ForLength(_folder.Bytes("tiledata.mul", bytes), bytes.Length);
            _raw = TileDataReader.ReadFile(format, out _)!;
            _table = TileDataReader.Load(format, out var error)!;
            Assert.That(error, Is.Empty);
        }

        [TearDown]
        public void TearDown() => _folder.Dispose();

        [TestCase(0x2A0)]
        [TestCase(0x3E0)]
        [TestCase(0x3E1)]
        [TestCase(0x7C9)]
        [TestCase(0x7CC)]
        [TestCase(0x7D0)]
        public void NoShootAdded(int id)
        {
            Assert.That(_table.Item(id).Flags, Is.EqualTo(_raw.Item(id).Flags | TileFlags.NoShoot));
            AssertRestUnchanged(id, heightToo: true);
        }

        [Test]
        public void LosBlocker_FlagsReplaced_Height20()
        {
            Assert.That(_table.Item(0x21A2).Flags, Is.EqualTo(TileFlags.Wall | TileFlags.NoShoot));
            Assert.That(_table.Item(0x21A2).Height, Is.EqualTo(20));
            AssertRestUnchanged(0x21A2, heightToo: false);
        }

        [TestCase(0x1940, 4)]
        [TestCase(0x34D2, 0)]
        public void HeightReplaced(int id, int height)
        {
            Assert.That(_table.Item(id).Height, Is.EqualTo(height));
            Assert.That(_table.Item(id).Flags, Is.EqualTo(_raw.Item(id).Flags));
            AssertRestUnchanged(id, heightToo: false);
        }

        [Test]
        public void OtherItemsAndLand_AsInFile()
        {
            foreach (var id in new[] { 0x29F, 0x7C8, 0x7D1, 0x1941, 0xFFFF })
            {
                Assert.That(_table.Item(id).Flags, Is.EqualTo(_raw.Item(id).Flags));
                Assert.That(_table.Item(id).Height, Is.EqualTo(_raw.Item(id).Height));
            }

            Assert.That(_table.Land(0x2A0).Flags, Is.EqualTo(_raw.Land(0x2A0).Flags));
        }

        private void AssertRestUnchanged(int id, bool heightToo)
        {
            var fixedItem = _table.Item(id);
            var raw = _raw.Item(id);
            Assert.That(fixedItem.Name, Is.EqualTo(raw.Name));
            Assert.That(fixedItem.Weight, Is.EqualTo(raw.Weight));
            Assert.That(fixedItem.Quality, Is.EqualTo(raw.Quality));
            Assert.That(fixedItem.Animation, Is.EqualTo(raw.Animation));
            Assert.That(fixedItem.Quantity, Is.EqualTo(raw.Quantity));
            Assert.That(fixedItem.Value, Is.EqualTo(raw.Value));
            if (heightToo)
            {
                Assert.That(fixedItem.Height, Is.EqualTo(raw.Height));
            }
        }
    }
}
