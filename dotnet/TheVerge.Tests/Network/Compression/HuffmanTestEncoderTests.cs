using System.Linq;
using NUnit.Framework;

namespace TheVerge.Tests.Network.Compression
{
    /// <summary>Тестовое сжатие даёт ровно байты сервера (ServerHuffmanSamples) — значит, ему можно верить в тестах приёма.</summary>
    public sealed class HuffmanTestEncoderTests
    {
        [Test]
        public void Compress_AllSymbols_SameBytesAsServer()
        {
            var symbols = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

            Assert.That(HuffmanTestEncoder.Compress(symbols), Is.EqualTo(ServerHuffmanSamples.AllSymbolsCompressed));
        }
    }
}
