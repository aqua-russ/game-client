using NUnit.Framework;
using TheVerge.Data.Files;

namespace TheVerge.Tests.Data.Files
{
    /// <summary>
    /// Хеш имён кусков UOP — hashlittle2 из lookup3 Боба Дженкинса (общественное достояние), как у сервера
    /// (UOPFiles.HashLittle2): старшие 32 бита — b, младшие — c. Эталоны — из проверочной программы lookup3.c.
    /// </summary>
    public sealed class UopHashTests
    {
        [Test]
        public void Empty_InitialValues()
        {
            Assert.That(UopHash.HashLittle2(string.Empty), Is.EqualTo(0xDEADBEEFDEADBEEFUL));
        }

        [Test]
        public void FourScore_LowPartMatchesReference()
        {
            // lookup3.c, driver5: hashlittle("Four score and seven years ago", 30, 0) = 0x17770551 — это c из hashlittle2.
            Assert.That((uint)UopHash.HashLittle2("Four score and seven years ago"), Is.EqualTo(0x17770551u));
        }

        [Test]
        public void AllTailLengths_Differ()
        {
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (var length = 0; length <= 26; length++)
            {
                Assert.That(seen.Add(UopHash.HashLittle2(new string('a', length))), Is.True, "длина " + length);
            }
        }
    }
}
