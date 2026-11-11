using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Compression;
using TheVerge.Tests.Network.Framing;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>
    /// Цепочка приёма без сокета (PLAN Стадия 1, п. 4; AGENTS.md §2.3, шаг 1): байты → Huffman (на игровом сокете
    /// после 0x91, IncomingAccountPackets.cs, GameLogin) → нарезка → очередь целых пакетов в буферах из пула.
    /// Сжатый поток — каждый пакет сжат отдельно, как у сервера (NetworkCompression.cs).
    /// </summary>
    public sealed class ReceivePipelineTests
    {
        [TestCase(int.MaxValue)]
        [TestCase(1)]
        [TestCase(5)]
        public void Uncompressed_AnyChunks_QueueHasServerPacketsInOrder(int chunk)
        {
            var expected = ServerPacketSamples.LoginStream();

            var received = Receive(ServerPacketSamples.Concat(expected), chunk, compressed: false);

            Assert.That(received, Is.EqualTo(expected));
        }

        [TestCase(int.MaxValue)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(17)]
        public void Compressed_AnyChunks_QueueHasServerPacketsInOrder(int chunk)
        {
            var expected = ServerPacketSamples.LoginStream();

            var received = Receive(HuffmanTestEncoder.CompressEach(expected), chunk, compressed: true);

            Assert.That(received, Is.EqualTo(expected));
        }

        [Test]
        public void CompressedLargePacket_ArrivesWhole()
        {
            var packet = new byte[20000];
            packet[0] = 0x1C;
            packet[1] = 0x4E;
            packet[2] = 0x20;
            for (var i = 3; i < packet.Length; i++)
            {
                packet[i] = (byte)(i * 7);
            }

            var received = Receive(HuffmanTestEncoder.Compress(packet), 1000, compressed: true);

            Assert.That(received, Is.EqualTo(new[] { packet }));
        }

        [Test]
        public void BrokenStream_GoodPacketsThenFailureWithDump_ThenNothing()
        {
            var queue = new InboundQueue(1 << 20);
            var pipeline = new ReceivePipeline(queue, new CountingArrayPool());

            pipeline.Receive(new byte[] { 0x55, 0x02, 0x09 });
            pipeline.Receive(new byte[] { 0x55 });

            Assert.That(pipeline.IsFailed, Is.True);
            Assert.That(queue.TryDequeue(out var first), Is.True);
            Assert.That(first.Kind, Is.EqualTo(InboundKind.Packet));
            Assert.That(queue.TryDequeue(out var failure), Is.True);
            Assert.That(failure.Kind, Is.EqualTo(InboundKind.FramingFailed));
            Assert.That(failure.Error.Kind, Is.EqualTo(FramingErrorKind.UnknownPacket));
            Assert.That(failure.Error.Dump, Is.EqualTo(new byte[] { 0x02, 0x09 }));
            Assert.That(queue.TryDequeue(out _), Is.False);
        }

        [Test]
        public void EveryPacketBuffer_ComesFromPool_AndGoesBack()
        {
            var pool = new CountingArrayPool();
            var queue = new InboundQueue(1 << 20);
            var pipeline = new ReceivePipeline(queue, pool);

            pipeline.Receive(ServerPacketSamples.Concat(ServerPacketSamples.LoginStream()));
            Assert.That(pool.Rented, Is.EqualTo(ServerPacketSamples.LoginStream().Count));

            while (queue.TryDequeue(out var item))
            {
                item.Release();
            }

            Assert.That(pool.Outstanding, Is.EqualTo(0));
        }

        private static List<byte[]> Receive(byte[] stream, int chunk, bool compressed)
        {
            var queue = new InboundQueue(1 << 24);
            var pipeline = new ReceivePipeline(queue, new CountingArrayPool());
            if (compressed)
            {
                pipeline.EnableCompression();
            }

            for (var offset = 0; offset < stream.Length; offset += chunk)
            {
                pipeline.Receive(stream.AsSpan(offset, Math.Min(chunk, stream.Length - offset)));
            }

            var received = new List<byte[]>();
            while (queue.TryDequeue(out var item))
            {
                Assert.That(item.Kind, Is.EqualTo(InboundKind.Packet), "ошибка нарезки: " + item.Error.Kind);
                received.Add(item.Packet.ToArray());
                item.Release();
            }

            return received;
        }
    }
}
