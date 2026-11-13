using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Игровая сессия (PLAN Стадия 1, п. 10). Сервер отключает клиента после 30 с без обмена данными
    /// (Projects/Server/Network/NetState/NetState.Network.cs: NextActivityCheck = curTicks + 30000; NetState.cs, CheckAlive);
    /// на 0x73 отвечает тем же номером (IncomingPlayerPackets.cs, PingReq → SendPingAck), на 0xD1 — 0xD1 (LogoutReq →
    /// SendLogoutAck), после чего соединение закрывает клиент.
    /// </summary>
    public sealed class GameSessionTests
    {
        private const int ServerIdleLimitMs = 30000;

        private RecordingSessionSender _sender = new RecordingSessionSender();
        private RecordingConnector _connector = new RecordingConnector();
        private FakeClock _clock = new FakeClock();
        private LoginFlow _flow = null!;
        private GameSession _session = null!;

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingSessionSender();
            _connector = new RecordingConnector();
            _clock = new FakeClock { NowMs = 1000 };
            _flow = new LoginFlow(new RecordingAccountSender(), _connector, 1);
            _session = new GameSession(_flow.Progress, _sender, _connector, _clock);
        }

        [Test]
        public void PingInterval_WellBelowServerLimit()
        {
            Assert.That(ClientLimits.PingIntervalMs * 3, Is.LessThanOrEqualTo(ServerIdleLimitMs));
        }

        [Test]
        public void NotInWorld_NoPing()
        {
            for (var t = 0; t <= 60000; t += 1000)
            {
                _session.Update(t);
            }

            Assert.That(_sender.Calls, Is.Empty);
        }

        [Test]
        public void InWorld_PingEveryInterval_NotMoreOften()
        {
            EnterWorld();
            _sender.Calls.Clear();
            var start = _clock.NowMs;

            for (var t = start; t <= start + 3 * ClientLimits.PingIntervalMs; t += 100)
            {
                _clock.NowMs = t;
                _session.Update(t);
            }

            var pings = _sender.Calls.Where(c => c.StartsWith("ping")).ToArray();
            Assert.That(pings.Length, Is.EqualTo(3));
            Assert.That(pings.Distinct().Count(), Is.EqualTo(3), "номер ping меняется");
        }

        [Test]
        public void PingEcho_SameNumber_GivesRtt_OtherNumberIgnored()
        {
            EnterWorld();
            _clock.NowMs += ClientLimits.PingIntervalMs;
            _session.Update(_clock.NowMs);
            var sequence = byte.Parse(_sender.Calls.Last().Split(' ')[1]);

            _clock.NowMs += 37;
            _session.OnPingAck(unchecked((byte)(sequence + 1)));
            Assert.That(_session.LastRttMs, Is.EqualTo(-1));

            _session.OnPingAck(sequence);
            Assert.That(_session.LastRttMs, Is.EqualTo(37));
        }

        [Test]
        public void Logout_AckThenClientCloses_ClosedWithoutError()
        {
            EnterWorld();
            _sender.Calls.Clear();

            _session.Logout();
            Assert.That(_sender.Calls, Is.EqualTo(new[] { "logout" }));
            Assert.That(_flow.State, Is.EqualTo(LoginState.LoggingOut));
            Assert.That(_connector.Disconnects, Is.EqualTo(0), "ждём ответа сервера");

            _session.OnLogoutAck();
            Assert.That(_connector.Disconnects, Is.EqualTo(1));
            _flow.OnDisconnected(DisconnectReason.Requested, string.Empty);

            Assert.That(_flow.State, Is.EqualTo(LoginState.Closed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
        }

        [Test]
        public void Logout_NoAck_ClosesAfterTimeout()
        {
            EnterWorld();
            _session.Logout();

            _clock.NowMs += ClientLimits.LogoutTimeoutMs - 1;
            _session.Update(_clock.NowMs);
            Assert.That(_connector.Disconnects, Is.EqualTo(0));

            _clock.NowMs += 1;
            _session.Update(_clock.NowMs);
            Assert.That(_connector.Disconnects, Is.EqualTo(1));
        }

        [Test]
        public void Logout_NotInWorld_Refused()
        {
            Assert.Throws<System.InvalidOperationException>(new System.Action(() => _session.Logout()));
            Assert.That(_sender.Calls, Is.Empty);
        }

        [Test]
        public void ServerClosesInWorld_Disconnected()
        {
            EnterWorld();

            _flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);

            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.Disconnected));
        }

        private void EnterWorld()
        {
            var world = new WorldEntry(_flow.Progress, _sender);
            _flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            _flow.SelectShard(0);
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            _flow.OnConnected();
            _flow.Characters.OnCharacterList(new[] { new CharacterSlot(0, "AQuaruss") }, new StartingCity[0], 0);
            _flow.Characters.Select(0);
            world.OnLoginConfirm(new PlayerState(new Serial(1), 0x190, Point3D.Zero, Direction.North, new MapSize(7168, 4096)));
            world.OnLoginComplete();
            _session.Update(_clock.NowMs);
        }
    }
}
