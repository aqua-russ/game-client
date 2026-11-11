using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>
    /// Очередь целых пакетов между фоновым потоком сокета и главным потоком (AGENTS.md §2.3). Предел в байтах —
    /// ClientLimits.InboundQueueBytes (ADR 0013 п. 5): полная очередь останавливает чтение сокета.
    /// </summary>
    public sealed class InboundQueueTests
    {
        private static readonly CountingArrayPool Pool = new CountingArrayPool();

        [Test]
        public void DefaultLimit_IsTwoMegabytes()
        {
            Assert.That(ClientLimits.InboundQueueBytes, Is.EqualTo(2 * 1024 * 1024));
        }

        [Test]
        public void Dequeue_InOrderOfArrival()
        {
            var queue = new InboundQueue(100);
            queue.Enqueue(Packet(0x55, 1));
            queue.Enqueue(Packet(0x73, 2));

            Assert.That(queue.TryDequeue(out var first), Is.True);
            Assert.That(queue.TryDequeue(out var second), Is.True);
            Assert.That(queue.TryDequeue(out _), Is.False);
            Assert.That(first.Packet[0], Is.EqualTo(0x55));
            Assert.That(second.Packet[0], Is.EqualTo(0x73));
        }

        [Test]
        public void QueuedBytes_CountPacketLengths()
        {
            var queue = new InboundQueue(100);
            queue.Enqueue(Packet(0x1B, 37));
            queue.Enqueue(Packet(0x55, 1));

            Assert.That(queue.QueuedBytes, Is.EqualTo(38));
            queue.TryDequeue(out _);
            Assert.That(queue.QueuedBytes, Is.EqualTo(1));
        }

        [Test]
        public void Full_WaitForSpaceTimesOut_UntilDequeued()
        {
            var queue = new InboundQueue(10);
            queue.Enqueue(Packet(0x01, 6));
            queue.Enqueue(Packet(0x02, 5));

            Assert.That(queue.IsFull, Is.True);
            using (var timeout = new CancellationTokenSource(50))
            {
                Assert.That(queue.WaitForSpace(timeout.Token), Is.False);
            }

            queue.TryDequeue(out _);
            Assert.That(queue.IsFull, Is.False);
            Assert.That(queue.WaitForSpace(CancellationToken.None), Is.True);
        }

        [Test]
        public void WaitForSpace_ReleasedByDequeueOnOtherThread()
        {
            var queue = new InboundQueue(4);
            queue.Enqueue(Packet(0x01, 4));
            var waiter = Task.Run(() => queue.WaitForSpace(CancellationToken.None));

            Thread.Sleep(30);
            Assert.That(waiter.IsCompleted, Is.False);
            queue.TryDequeue(out _);

            Assert.That(waiter.Wait(2000), Is.True);
            Assert.That(waiter.Result, Is.True);
        }

        [Test]
        public void Clear_ReturnsAllBuffersAndEmpties()
        {
            var pool = new CountingArrayPool();
            var queue = new InboundQueue(100);
            queue.Enqueue(InboundEvent.FromPacket(pool.Rent(3), 3, pool));
            queue.Enqueue(InboundEvent.Closed("тест"));
            queue.Enqueue(InboundEvent.FromPacket(pool.Rent(2), 2, pool));

            queue.Clear();

            Assert.That(pool.Outstanding, Is.EqualTo(0));
            Assert.That(queue.TryDequeue(out _), Is.False);
            Assert.That(queue.QueuedBytes, Is.EqualTo(0));
        }

        [Test]
        public void ProducerAndConsumerThreads_NothingLostOrReordered()
        {
            const int count = 20000;
            var queue = new InboundQueue(1000);
            var pool = new CountingArrayPool();
            var producer = Task.Run(() =>
            {
                for (var i = 0; i < count; i++)
                {
                    queue.WaitForSpace(CancellationToken.None);
                    var buffer = pool.Rent(2);
                    buffer[0] = (byte)(i >> 8);
                    buffer[1] = (byte)i;
                    queue.Enqueue(InboundEvent.FromPacket(buffer, 2, pool));
                }
            });

            var next = 0;
            var deadline = Stopwatch.StartNew();
            while (next < count)
            {
                Assert.That(deadline.ElapsedMilliseconds, Is.LessThan(10000), "получено " + next + " из " + count);
                if (!queue.TryDequeue(out var item))
                {
                    Thread.Yield();
                    continue;
                }

                Assert.That((item.Packet[0] << 8) | item.Packet[1], Is.EqualTo(next & 0xFFFF));
                item.Release();
                next++;
            }

            Assert.That(producer.Wait(5000), Is.True);
            Assert.That(pool.Outstanding, Is.EqualTo(0));
        }

        private static InboundEvent Packet(byte id, int length)
        {
            var buffer = new byte[length];
            buffer[0] = id;
            return InboundEvent.FromPacket(buffer, length, Pool);
        }
    }
}
