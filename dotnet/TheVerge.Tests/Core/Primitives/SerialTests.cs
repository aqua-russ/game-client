using NUnit.Framework;
using TheVerge.Core.Primitives;

namespace TheVerge.Tests.Core.Primitives
{
    /// <summary>
    /// Границы номеров объектов — как у сервера: Projects/Server/World/World.cs (ItemOffset = 0x40000000,
    /// MaxVirtualSerial = 0x7FFFFFFF) и Projects/Server/Serial.cs. Обычные предметы сервера кончаются на 0x7EEEEEEE,
    /// выше — виртуальные предметы (World.NewVirtual), которые тоже приходят клиенту как предметы: например,
    /// заклинания в книге (serial 0x7FFFFFFF − i, docs/TARGET.md §2). Поэтому у клиента предметы — до 0x7FFFFFFF.
    /// </summary>
    public sealed class SerialTests
    {
        [TestCase(0x00000000u, false, false, false)]
        [TestCase(0x00000001u, true, true, false)]
        [TestCase(0x3FFFFFFFu, true, true, false)]
        [TestCase(0x40000000u, true, false, true)]
        [TestCase(0x7EEEEEEEu, true, false, true)]
        [TestCase(0x7FFFFFFFu, true, false, true)]
        [TestCase(0x80000000u, true, false, false)]
        [TestCase(0xFFFFFFFFu, true, false, false)]
        public void Kind_MatchesServerRanges(uint value, bool isValid, bool isMobile, bool isItem)
        {
            var serial = new Serial(value);

            Assert.That(serial.IsValid, Is.EqualTo(isValid), "IsValid");
            Assert.That(serial.IsMobile, Is.EqualTo(isMobile), "IsMobile");
            Assert.That(serial.IsItem, Is.EqualTo(isItem), "IsItem");
        }

        [Test]
        public void Equality_ComparesValue()
        {
            Assert.That(new Serial(0x40000005) == new Serial(0x40000005), Is.True);
            Assert.That(new Serial(0x40000005) != new Serial(0x40000006), Is.True);
        }
    }
}
