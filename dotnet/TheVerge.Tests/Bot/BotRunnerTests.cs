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
    /// Бот (PLAN Стадия 1, п. 15): собирает TcpConnection → NetworkSession → GameClient и вход LoginFlow, крутит тики и
    /// честно говорит, чем всё кончилось, — сообщением и кодом выхода. Сразу после подключения бот шлёт seed и логин:
    /// сервер блокирует адрес, если подключение 5 с молчит (Projects/Server/Network/NetState/NetState.Network.cs,
    /// DisconnectUnattachedSockets, BanReasons.SilentConnect). Отказ 0x82 — OutgoingAccountPackets.cs, ALRReason.
    /// </summary>
    public sealed class BotRunnerTests
    {
        private const int ServerSilenceLimitMs = 5000;

        /// <summary>0xEF (21 байт) и 0x80 (62 байта): IncomingAccountPackets.cs, Register(0xEF, …, 21) и Register(0x80, …, 62).</summary>
        private const int SeedAndLoginLength = 21 + 62;

        private const string Password = "s3cretPw";

        private RecordingLog _log = new RecordingLog();
        private FakeClock _clock = new FakeClock();

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLog();
            _clock = new FakeClock();
        }

        [Test]
        public void ServerUp_SeedAndLoginArriveLongBeforeServerLimit()
        {
            using (var server = new LocalServer())
            {
                var run = Task.Run(() => NewRunner(NewConnection()).Run(Target(server.Port), CancellationToken.None));
                var watch = Stopwatch.StartNew();
                server.Accept();

                var received = server.ReadExactly(SeedAndLoginLength);

                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(ServerSilenceLimitMs / 5));
                Assert.That(received[0], Is.EqualTo(0xEF));
                Assert.That(received[21], Is.EqualTo(0x80));
                server.Client.Send(new byte[] { 0x82, 0x03 });
                server.CloseClient();
                Assert.That(run.Wait(5000), Is.True, "бот не закончил работу");
                Assert.That(run.Result, Is.EqualTo(BotExitCode.LoginRejected), LogText());
            }
        }

        [Test]
        public void WrongPassword_LoginRejected_ReasonInWords_PasswordNowhere()
        {
            var connection = new FakeConnection();
            connection.Push(InboundEvent.Connected());
            connection.PushAfterSend(InboundEvent.FromPacket(new byte[] { 0x82, 0x03 }, 2, new CountingArrayPool()));
            connection.PushAfterSend(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));

            var code = NewRunner(connection).Run(Target(2593), CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.LoginRejected));
            Assert.That(_log.Errors.Single(), Does.Contain("неверный пароль"));
            Assert.That(LogText(), Does.Not.Contain(Password));
        }

        [Test]
        public void PacketLog_ConnectedLoginRejectedClosed_PasswordMasked()
        {
            var output = new StringWriter();
            var packetLog = new PacketLogWriter(output, _clock, _log, long.MaxValue);
            var connection = new FakeConnection();
            connection.Push(InboundEvent.Connected());
            connection.PushAfterSend(InboundEvent.FromPacket(new byte[] { 0x82, 0x03 }, 2, new CountingArrayPool()));
            connection.PushAfterSend(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));

            new BotRunner(() => connection, _clock, _log, packetLog).Run(Target(2593), CancellationToken.None);

            var reader = new PacketLogReader(new StringReader(output.ToString()));
            var kinds = new System.Collections.Generic.List<string>();
            while (reader.TryRead(out var entry))
            {
                kinds.Add(entry.Kind == PacketLogKind.Packet ? entry.Direction + " 0x" + entry.Packet[0].ToString("X2") : entry.Kind.ToString());
            }

            Assert.That(kinds, Is.EqualTo(new[]
            {
                "Connected", "ClientToServer 0xEF", "ClientToServer 0x80", "ServerToClient 0x82", "Closed",
            }));
            Assert.That(output.ToString(), Does.Not.Contain(HexOf(Password)));
        }

        [Test]
        public void NothingListens_ConnectFailed()
        {
            var code = NewRunner(NewConnection()).Run(Target(FreePort()), CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectFailed));
            Assert.That(_log.Errors.Single(), Does.Contain("Не удалось подключиться").And.Contain("сервер"));
        }

        [Test]
        public void ServerNeverAnswers_TimeoutIsConnectFailed()
        {
            var connection = TcpConnectionTestKit.NewSilentServerConnection(TcpConnectionTestKit.ShortTimeoutMs);

            var code = NewRunner(connection).Run(Target(TcpConnectionTestKit.SilentServerPort), CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectFailed));
            Assert.That(_log.Errors.Single(), Does.Contain("не ответил"));
        }

        [Test]
        public void ServerClosesConnection_ConnectionLost()
        {
            var connection = new FakeConnection();
            connection.Push(InboundEvent.Connected());
            connection.Push(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));

            var code = NewRunner(connection).Run(Target(2593), CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectionLost));
            Assert.That(_log.Errors.Single(), Does.Contain("Сервер закрыл соединение"));
        }

        [Test]
        public void SocketError_ConnectionLost()
        {
            var connection = new FakeConnection();
            connection.Push(InboundEvent.Closed(ConnectionCloseReason.SocketError, "Connection reset"));

            var code = NewRunner(connection).Run(Target(2593), CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectionLost));
            Assert.That(_log.Errors.Single(), Does.Contain("Connection reset"));
        }

        [Test]
        public void BrokenStream_ProtocolError()
        {
            var connection = new FakeConnection();
            var error = new FramingError(FramingErrorKind.UnknownPacket, 0x02, 0, 0, new byte[] { 0x02 });
            connection.Push(InboundEvent.FramingFailed(error));

            var code = NewRunner(connection).Run(Target(2593), CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.ProtocolError));
        }

        [Test]
        public void CtrlC_WhileConnecting_InterruptedQuickly()
        {
            var connection = TcpConnectionTestKit.NewSilentServerConnection(ClientLimits.ConnectTimeoutMs);
            var runner = new BotRunner(() => connection, new SystemClockForTests(), _log, null);
            using (var interrupt = new CancellationTokenSource())
            {
                var watch = Stopwatch.StartNew();
                var run = Task.Run(() => runner.Run(Target(TcpConnectionTestKit.SilentServerPort), interrupt.Token));
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
            Assert.That(BotExitCode.LoginRejected, Is.EqualTo(6));
            Assert.That(BotExitCode.Interrupted, Is.EqualTo(130));
        }

        private BotRunner NewRunner(IConnection connection) => new BotRunner(() => connection, new SystemClockForTests(), _log, null);

        private static BotLogin Target(int port) => new BotLogin("127.0.0.1", port, new LoginCredentials("player", Password), string.Empty);

        private static string HexOf(string text) =>
            string.Join(" ", System.Text.Encoding.ASCII.GetBytes(text).Select(b => b.ToString("X2")));

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
