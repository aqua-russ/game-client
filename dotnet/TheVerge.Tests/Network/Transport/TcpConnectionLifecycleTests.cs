using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Framing;
using static TheVerge.Tests.Network.Transport.TcpConnectionTestKit;

namespace TheVerge.Tests.Network.Transport
{
    /// <summary>
    /// Транспорт на настоящем сокете — подключение и закрытие (PLAN Стадия 1, п. 4): таймаут подключения
    /// (ClientLimits.ConnectTimeoutMs, ADR 0013), закрытие сервером, отмена через CancellationToken, Dispose.
    /// Сервер — локальный TcpListener теста, без ModernUO.
    /// </summary>
    public sealed class TcpConnectionLifecycleTests
    {
        [Test]
        public void ConnectTimeout_IsTenSeconds()
        {
            Assert.That(ClientLimits.ConnectTimeoutMs, Is.EqualTo(10000));
        }

        [Test]
        public void ServerCloses_ClosedEvent_StateClosed()
        {
            using (var server = new LocalServer())
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                server.Accept();
                server.Client.Send(new byte[] { 0x55 });
                server.CloseClient();

                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Until(InboundKind.Closed));

                Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[] { InboundKind.Connected, InboundKind.Packet, InboundKind.Closed }));
                Assert.That(LastCloseReason(events), Is.EqualTo(ConnectionCloseReason.ServerClosed));
                Assert.That(WaitForState(connection, ConnectionState.Closed), Is.True);
            }
        }

        [Test]
        public void NoAnswer_ConnectTimeout()
        {
            using (var connection = NewSilentServerConnection(ShortTimeoutMs))
            {
                var watch = Stopwatch.StartNew();
                connection.Connect("127.0.0.1", SilentServerPort, CancellationToken.None);

                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Until(InboundKind.Closed));

                Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[] { InboundKind.Closed }));
                Assert.That(LastCloseReason(events), Is.EqualTo(ConnectionCloseReason.ConnectTimeout));
                Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(ShortTimeoutMs - 50).And.LessThan(3000));
            }
        }

        [Test]
        public void NothingListening_ConnectFailed()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", port, CancellationToken.None);

                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Until(InboundKind.Closed));

                Assert.That(LastCloseReason(events), Is.EqualTo(ConnectionCloseReason.ConnectFailed));
            }
        }

        [Test]
        public void CancelDuringConnect_ClosedQuickly()
        {
            using (var cancel = new CancellationTokenSource())
            using (var connection = NewSilentServerConnection(ClientLimits.ConnectTimeoutMs))
            {
                connection.Connect("127.0.0.1", SilentServerPort, cancel.Token);
                Thread.Sleep(50);
                var watch = Stopwatch.StartNew();
                cancel.Cancel();

                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Until(InboundKind.Closed));

                Assert.That(LastCloseReason(events), Is.EqualTo(ConnectionCloseReason.Cancelled));
                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(2000));
            }
        }

        [Test]
        public void CancelWhileConnected_ClosedAndServerSeesClose()
        {
            using (var server = new LocalServer())
            using (var cancel = new CancellationTokenSource())
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", server.Port, cancel.Token);
                server.Accept();
                ConnectionEvents.Collect(connection, ConnectionEvents.Count(1));

                cancel.Cancel();
                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Until(InboundKind.Closed));

                Assert.That(LastCloseReason(events), Is.EqualTo(ConnectionCloseReason.Cancelled));
                Assert.That(server.SeesClientClosed(), Is.True);
            }
        }

        [Test]
        public void CancelWhileReading_EndOfStreamAfterOwnClose_IsCancelledNotServerClosed()
        {
            // Своё закрытие сокета будит ждущее чтение, и оно может вернуть «0 байт» раньше, чем сокет закроется
            // окончательно. Это не сервер закрыл соединение: отмену запросили мы. Подстановка чтения делает порядок
            // «отмена, затем 0 байт» постоянным, а не случайным.
            using (var server = new LocalServer())
            using (var cancel = new CancellationTokenSource())
            using (var connection = NewConnectionReadingEndAfter(cancel.Token))
            {
                connection.Connect("127.0.0.1", server.Port, cancel.Token);
                server.Accept();
                ConnectionEvents.Collect(connection, ConnectionEvents.Count(1));

                cancel.Cancel();
                var events = ConnectionEvents.Collect(connection, ConnectionEvents.Until(InboundKind.Closed));

                Assert.That(LastCloseReason(events), Is.EqualTo(ConnectionCloseReason.Cancelled));
            }
        }

        [Test]
        public void Dispose_WhileConnected_ServerSeesClose_BuffersReturned()
        {
            var pool = new CountingArrayPool();
            using (var server = new LocalServer())
            {
                var connection = new TcpConnection(pool, ShortTimeoutMs, 1 << 20);
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);
                server.Accept();
                server.SendInChunks(ServerPacketSamples.Concat(ServerPacketSamples.LoginStream()), 1000);
                Assert.That(WaitFor(() => pool.Rented == ServerPacketSamples.LoginStream().Count), Is.True);

                connection.Dispose();
                connection.Dispose();

                Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
                Assert.That(pool.Outstanding, Is.EqualTo(0));
                Assert.That(server.SeesClientClosed(), Is.True);
                Assert.That(connection.Send(new byte[] { 0x73, 0x00 }), Is.False);
            }
        }

        [Test]
        public void Dispose_DuringConnect_ReturnsQuickly()
        {
            var connection = NewSilentServerConnection(ClientLimits.ConnectTimeoutMs);
            connection.Connect("127.0.0.1", SilentServerPort, CancellationToken.None);
            Thread.Sleep(50);

            var watch = Stopwatch.StartNew();
            connection.Dispose();

            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(2000));
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        }

        [Test]
        public void Connect_Twice_Throws()
        {
            using (var server = new LocalServer())
            using (var connection = NewConnection())
            {
                connection.Connect("127.0.0.1", server.Port, CancellationToken.None);

                Assert.Throws<InvalidOperationException>(new Action(() =>
                    connection.Connect("127.0.0.1", server.Port, CancellationToken.None)));
            }
        }
    }
}
