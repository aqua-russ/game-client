using NUnit.Framework;
using TheVerge.Network.Session;

namespace TheVerge.Tests.Network.Session
{
    /// <summary>Дамп байтов для лога (AGENTS.md §3.5): шестнадцатеричные пары через пробел, как в логе пакетов (PLAN п. 12).</summary>
    public sealed class PacketDumpTests
    {
        [Test]
        public void ToHex_BytesSeparatedBySpace()
        {
            Assert.That(PacketDump.ToHex(new byte[] { 0x02, 0xAB, 0x00, 0xFF }), Is.EqualTo("02 AB 00 FF"));
        }

        [Test]
        public void ToHex_Empty_IsEmpty()
        {
            Assert.That(PacketDump.ToHex(new byte[0]), Is.EqualTo(string.Empty));
        }
    }
}
