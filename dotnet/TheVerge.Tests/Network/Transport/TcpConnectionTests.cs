using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Compression;
using TheVerge.Tests.Network.Framing;
using static TheVerge.Tests.Network.Transport.TcpConnectionTestKit;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>
    /// Транспорт на настоящем сокете — данные (PLAN Стадия 1, п. 4): фоновый поток только читает, распаковывает, режет
    /// и кладёт целые пакеты в очередь (AGENTS.md §2.3); отправка без блокировки; полная очередь останавливает чтение
    /// (ADR 0013 п. 5). Сервер — локальный TcpListener теста, без ModernUO.
    /// </summary>
    public sealed class TcpConnectionTests
    {
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1000)]
        public void ServerSendsPackets_ConnectedThenSamePacketsInOrder(int chunk)
        {
            var expected = ServerPacketSamples.LoginStream();
            using (var server = new LocalServer())
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                server.Accept();
                server.SendInChunks(ServerPacketSamples.Concat(expected), chunk);

                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Count(1 + expected.Count));

                Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[] { InboundKind.Connected }
                    .Concat(Enumerable.Repeat(InboundKind.Packet, expected.Count))));
                Assert.That(events.Skip(1).Select(e => e.Packet), Is.EqualTo(expected));
                Assert.That(connection.State, Is.EqualTo(ConnectionState.Connected));
            }
        }

        [Test]
        public void CompressionEnabled_CompressedStream_SamePackets()
        {
            var expected = ServerPacketSamples.LoginStream();
            using (var server = new LocalServer())
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                server.Accept();
                ConnectionEvents.Collect(connection, ConnectionEvents.Count(1));
                connection.EnableCompression();
                server.SendInChunks(HuffmanTestEncoder.CompressEach(expected), 3);

                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Count(expected.Count));

                Assert.That(events.Select(e => e.Packet), Is.EqualTo(expected));
            }
        }

        [Test]
        public void Send_ServerReceivesSameBytesInOrder()
        {
            using (var server = new LocalServer())
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                server.Accept();
                ConnectionEvents.Collect(connection, ConnectionEvents.Count(1));

                Assert.That(connection.Send(new byte[] { 0xEF, 0x01, 0x02 }), Is.True);
                Assert.That(connection.Send(new byte[] { 0x73, 0x05 }), Is.True);

                Assert.That(server.ReadExactly(5), Is.EqualTo(new byte[] { 0xEF, 0x01, 0x02, 0x73, 0x05 }));
            }
        }

        [Test]
        public void Send_BeforeConnect_ReturnsFalse()
        {
            using (var connection = NewConnection())
            {
                Assert.That(connection.Send(new byte[] { 0x73, 0x00 }), Is.False);
                Assert.That(connection.State, Is.EqualTo(ConnectionState.Idle));
            }
        }

        [Test]
        public void Send_ServerDoesNotRead_DoesNotBlock()
        {
            var chunk = new byte[1024 * 1024];
            using (var server = new LocalServer())
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                server.Accept();
                ConnectionEvents.Collect(connection, ConnectionEvents.Count(1));

                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 16; i++)
                {
                    Assert.That(connection.Send(chunk), Is.True);
                }

                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(1000));
            }
        }

        [Test]
        public void FullQueue_StopsReading_ThenAllPacketsArrive()
        {
            const int packets = 4000;
            const int packetLength = 1000;
            var packet = new byte[packetLength];
            packet[0] = 0x1C;
            packet[1] = packetLength >> 8;
            packet[2] = packetLength & 0xFF;
            var stream = Enumerable.Range(0, packets).SelectMany(_ => packet).ToArray();
            using (var server = new LocalServer())
            using (var connection = new TcpConnection(new CountingArrayPool(), ShortTimeoutMs, 1000))
            {
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                server.Accept();
                var sending = Task.Run(() => server.SendInChunks(stream, 64 * 1024));

                Thread.Sleep(500);
                Assert.That(sending.IsCompleted, Is.False, "сервер успел отправить всё: чтение не остановилось");
                Assert.That(connection.InboundQueuedBytes, Is.LessThan(200 * 1024));

                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Count(1 + packets), 20000);

                Assert.That(events.Count(e => e.Kind == InboundKind.Packet), Is.EqualTo(packets));
                Assert.That(sending.Wait(5000), Is.True);
            }
        }
    }
}
