using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Network.Compression;

namespace TheVerge.Tests.Network.Compression
{
    /// <summary>
    /// Распаковка сжатого потока сервер → клиент (PLAN Стадия 1, п. 3). Ожидания — байты сервера
    /// (ServerHuffmanSamples), а не круговой прогон «сжал своим кодом — распаковал своим кодом»:
    /// такой прогон не ловит ошибку, общую для сжатия и распаковки.
    /// TCP режет поток где угодно, поэтому тот же поток подаётся кусками разной длины.
    /// </summary>
    public sealed class HuffmanDecoderTests
    {
        [Test]
        public void Decode_ServerSample_ReturnsAllSymbols()
        {
            var restored = DecodeInChunks(ServerHuffmanSamples.AllSymbolsCompressed, int.MaxValue);

            Assert.That(restored, Is.EqualTo(ServerHuffmanSamples.AllSymbols()));
        }

        [Test]
        public void Decode_ServerSampleByOneByte_ReturnsAllSymbols()
        {
            var restored = DecodeInChunks(ServerHuffmanSamples.AllSymbolsCompressed, 1);

            Assert.That(restored, Is.EqualTo(ServerHuffmanSamples.AllSymbols()));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Decode_ServerSampleInRandomChunks_ReturnsAllSymbols(int seed)
        {
            var restored = DecodeInRandomChunks(ServerHuffmanSamples.AllSymbolsCompressed, new Random(seed));

            Assert.That(restored, Is.EqualTo(ServerHuffmanSamples.AllSymbols()));
        }

        [Test]
        public void Decode_TwoPacketsInOneRead_ReturnsBoth()
        {
            var sample = ServerHuffmanSamples.AllSymbolsCompressed;
            var twoPackets = new byte[sample.Length * 2];
            sample.CopyTo(twoPackets, 0);
            sample.CopyTo(twoPackets, sample.Length);
            var expected = new List<byte>(ServerHuffmanSamples.AllSymbols());
            expected.AddRange(ServerHuffmanSamples.AllSymbols());

            var restored = DecodeInChunks(twoPackets, int.MaxValue);

            Assert.That(restored, Is.EqualTo(expected));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(100)]
        public void Decode_OutputBufferSmallerThanData_ContinuesWithoutLoss(int outputSize)
        {
            var decoder = new HuffmanDecoder();
            var compressed = ServerHuffmanSamples.AllSymbolsCompressed;
            var output = new List<byte>();
            var buffer = new byte[outputSize];
            int offset = 0;
            for (int call = 0; call < 10_000; call++)
            {
                int written = decoder.Decode(compressed.AsSpan(offset), buffer, out int consumed);
                output.AddRange(buffer.AsSpan(0, written).ToArray());
                offset += consumed;
                if (offset == compressed.Length && written < buffer.Length)
                {
                    break;
                }
            }

            Assert.That(output, Is.EqualTo(ServerHuffmanSamples.AllSymbols()));
        }

        private static byte[] DecodeInRandomChunks(byte[] compressed, Random random)
        {
            var decoder = new HuffmanDecoder();
            var output = new List<byte>();
            int offset = 0;
            while (offset < compressed.Length)
            {
                int size = Math.Min(random.Next(1, 40), compressed.Length - offset);
                Feed(decoder, compressed.AsSpan(offset, size), output);
                offset += size;
            }

            return output.ToArray();
        }

        private static byte[] DecodeInChunks(byte[] compressed, int chunkSize)
        {
            var decoder = new HuffmanDecoder();
            var output = new List<byte>();
            for (int offset = 0; offset < compressed.Length; offset += chunkSize)
            {
                int size = Math.Min(chunkSize, compressed.Length - offset);
                Feed(decoder, compressed.AsSpan(offset, size), output);
            }

            return output.ToArray();
        }

        private static void Feed(HuffmanDecoder decoder, ReadOnlySpan<byte> chunk, List<byte> output)
        {
            // Самый короткий код — 2 бита, поэтому из байта выходит не больше 4 символов: места хватает с запасом.
            var buffer = new byte[chunk.Length * 4 + 4];
            int written = decoder.Decode(chunk, buffer, out int consumed);
            Assert.That(consumed, Is.EqualTo(chunk.Length), "распаковка остановилась, не дочитав вход");
            output.AddRange(buffer.AsSpan(0, written).ToArray());
        }
    }
}
