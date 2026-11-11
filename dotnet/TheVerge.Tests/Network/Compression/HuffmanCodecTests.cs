using System;
using NUnit.Framework;
using TheVerge.Network;

namespace TheVerge.Tests.Network.Compression
{
    /// <summary>
    /// Первые тесты кода прототипа вне Unity. Проверяют согласованность сжатия и распаковки и то, что
    /// распаковка переживает разрыв потока посреди байта (TCP режет данные где угодно).
    /// Сверка с реальными байтами сервера — на golden-логах (задача 0.6, Стадия 1).
    /// </summary>
    public sealed class HuffmanCodecTests
    {
        [Test]
        public void CompressThenDecompress_ReturnsOriginalBytes()
        {
            var original = SampleBytes();
            var compressed = Compress(original);

            var restored = Decompress(new HuffmanCodec(), compressed);

            Assert.That(restored, Is.EqualTo(original));
        }

        [Test]
        public void Decompress_StreamSplitIntoSingleBytes_ReturnsOriginalBytes()
        {
            var original = SampleBytes();
            var compressed = Compress(original);
            var codec = new HuffmanCodec();
            var restored = new byte[original.Length];
            int total = 0;

            for (int i = 0; i < compressed.Length; i++)
            {
                bool ok = codec.Decompress(compressed.AsSpan(i, 1), restored.AsSpan(total), out int written);
                Assert.That(ok, Is.True, "буфер распаковки переполнен");
                total += written;
            }

            Assert.That(total, Is.EqualTo(original.Length));
            Assert.That(restored, Is.EqualTo(original));
        }

        private static byte[] SampleBytes()
        {
            var bytes = new byte[512];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i * 7);
            }

            return bytes;
        }

        private static byte[] Compress(byte[] source)
        {
            var buffer = new byte[source.Length * 2 + 16];
            bool ok = HuffmanCodec.Compress(source, buffer, out int written);
            Assert.That(ok, Is.True, "буфер сжатия переполнен");
            return buffer.AsSpan(0, written).ToArray();
        }

        private static byte[] Decompress(HuffmanCodec codec, byte[] compressed)
        {
            var buffer = new byte[compressed.Length * 8];
            bool ok = codec.Decompress(compressed, buffer, out int written);
            Assert.That(ok, Is.True, "буфер распаковки переполнен");
            return buffer.AsSpan(0, written).ToArray();
        }
    }
}
