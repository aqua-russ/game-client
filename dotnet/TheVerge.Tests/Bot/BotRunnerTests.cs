using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Каркас бота (PLAN Стадия 1, п. 15): собирает TcpConnection → NetworkSession → GameClient, крутит тики и честно
    /// говорит, чем всё кончилось, — сообщением и кодом выхода. Вход на сервер — задачи 1.5–1.9; пока бот, подключившись,
    /// сразу закрывает соединение сам: сервер блокирует адрес, если подключение 5 с молчит
    /// (Projects/Server/Network/NetState/NetState.Network.cs, DisconnectUnattachedSockets, BanReasons.SilentConnect).
    /// </summary>
    public sealed class BotRunnerTests
    {
        private const int ServerSilenceLimitMs = 5000;

        private RecordingLog _log = new RecordingLog();
        private FakeClock _clock = new FakeClock();

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLog();
            _clock = new FakeClock();
        }

        [Test]
        public void ServerUp_ConnectsThenClosesItselfLongBeforeServerLimit_Success()
        {
            using (var server = new LocalServer())
            {
                var watch = Stopwatch.StartNew();

                var code = NewRunner(NewConnection()).Run("127.0.0.1", server.Port, CancellationToken.None);

                Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(ServerSilenceLimitMs / 5));
                server.Accept();
                Assert.That(server.SeesClientClosed(), Is.True);
                Assert.That(Infos(), Does.Contain("Подключено").And.Contain("1.5"));
            }
        }

        [Test]
        public void ServerUp_PacketLogHasConnectedAndClosed()
        {
            using (var server = new LocalServer())
            {
                var output = new StringWriter();
                var packetLog = new PacketLogWriter(output, _clock, _log, long.MaxValue);

                new BotRunner(NewConnection(), _clock, _log, packetLog).Run("127.0.0.1", server.Port, CancellationToken.None);

                var reader = new PacketLogReader(new StringReader(output.ToString()));
                Assert.That(reader.TryRead(out var connected) && connected.Kind == PacketLogKind.Connected, Is.True);
                Assert.That(reader.TryRead(out var closed) && closed.Kind == PacketLogKind.Closed, Is.True);
            }
        }

        [Test]
        public void NothingListens_ConnectFailed()
        {
            var code = NewRunner(NewConnection()).Run("127.0.0.1", FreePort(), CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectFailed));
            Assert.That(_log.Errors.Single(), Does.Contain("Не удалось подключиться").And.Contain("сервер"));
        }

        [Test]
        public void ServerNeverAnswers_TimeoutIsConnectFailed()
        {
            var connection = TcpConnectionTestKit.NewSilentServerConnection(TcpConnectionTestKit.ShortTimeoutMs);

            var code = NewRunner(connection).Run("127.0.0.1", TcpConnectionTestKit.SilentServerPort, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectFailed));
            Assert.That(_log.Errors.Single(), Does.Contain("не ответил"));
        }

        [Test]
        public void ServerClosesConnection_ConnectionLost()
        {
            var connection = new FakeConnection();
            connection.Push(InboundEvent.Connected());
            connection.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));

            var code = NewRunner(connection).Run("127.0.0.1", 2593, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectionLost));
            Assert.That(_log.Errors.Single(), Does.Contain("Сервер закрыл соединение"));
        }

        [Test]
        public void SocketError_ConnectionLost()
        {
            var connection = new FakeConnection();
            connection.Push(InboundEvent.Closed(ConnectionCloseReason.SocketError, "Connection reset"));

            var code = NewRunner(connection).Run("127.0.0.1", 2593, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectionLost));
            Assert.That(_log.Errors.Single(), Does.Contain("Connection reset"));
        }

        [Test]
        public void BrokenStream_ProtocolError()
        {
            var connection = new FakeConnection();
            var error = new FramingError(FramingErrorKind.UnknownPacket, 0x02, 0, 0, new byte[] { 0x02 });
            connection.Push(InboundEvent.FramingFailed(error));

            var code = NewRunner(connection).Run("127.0.0.1", 2593, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ProtocolError));
        }

        [Test]
        public void CtrlC_WhileConnecting_InterruptedQuickly()
        {
            var connection = TcpConnectionTestKit.NewSilentServerConnection(ClientLimits.ConnectTimeoutMs);
            var runner = new BotRunner(connection, new SystemClockForTests(), _log, null);
            using (var interrupt = new CancellationTokenSource())
            {
                var watch = Stopwatch.StartNew();
                var run = Task.Run(() => runner.Run("127.0.0.1", TcpConnectionTestKit.SilentServerPort, interrupt.Token));
                Thread.Sleep(100);

                interrupt.Cancel();

                Assert.That(run.Wait(2000), Is.True, "бот не остановился по Ctrl+C");
                Assert.That(run.Result, Is.EqualTo(BotExitCode.Interrupted));
                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(ClientLimits.ConnectTimeoutMs));
                Assert.That(Infos(), Does.Contain("Ctrl+C"));
            }
        }

        [Test]
        public void ExitCodes_AsAgreed()
        {
            Assert.That(BotExitCode.Success, Is.EqualTo(0));
            Assert.That(BotExitCode.BadArguments, Is.EqualTo(1));
            Assert.That(BotExitCode.ConnectFailed, Is.EqualTo(2));
            Assert.That(BotExitCode.ConnectionLost, Is.EqualTo(3));
            Assert.That(BotExitCode.ProtocolError, Is.EqualTo(4));
            Assert.That(BotExitCode.InternalError, Is.EqualTo(5));
            Assert.That(BotExitCode.Interrupted, Is.EqualTo(130));
        }

        private BotRunner NewRunner(IConnection connection) => new BotRunner(connection, new SystemClockForTests(), _log, null);

        private static TcpConnection NewConnection() =>
            new TcpConnection(new CountingArrayPool(), ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes);

        private string LogText() => string.Join("\n", _log.Entries.Select(e => e.Level + ": " + e.Message));

        private string Infos() => string.Join("\n", _log.Entries.Where(e => e.Level == "info").Select(e => e.Message));

        // Свободный порт: занять у ОС и сразу отпустить — подключение к нему получит отказ.
        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private sealed class SystemClockForTests : TheVerge.Core.Ports.IClock
        {
            private readonly Stopwatch _watch = Stopwatch.StartNew();

            public long NowMs => _watch.ElapsedMilliseconds;
        }
    }
}
