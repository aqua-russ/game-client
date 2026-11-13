using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Повтор входа с нарастающей паузой (PLAN Стадия 1, «Реакция клиента на ошибки»: таймаут подключения — повтор с
    /// нарастающей паузой, затем ошибка; разрыв в мире — переподключение с нарастающей паузой). Паузы — ClientLimits.
    /// </summary>
    public sealed class ReconnectorTests
    {
        private RecordingAccountSender _sender = new RecordingAccountSender();
        private RecordingConnector _connector = new RecordingConnector();
        private LoginFlow _flow = null!;
        private Reconnector _reconnector = null!;
        private List<(int Attempt, int Delay)> _scheduled = new List<(int, int)>();

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingAccountSender();
            _connector = new RecordingConnector();
            _flow = new LoginFlow(_sender, _connector, 1);
            _reconnector = new Reconnector(_flow);
            _scheduled = new List<(int, int)>();
            _reconnector.RetryScheduled += (a, d) => _scheduled.Add((a, d));
        }

        [Test]
        public void ConnectFails_RetriesWithGrowingPause_ThenGivesUp()
        {
            _flow.Start("127.0.0.1", 2593, Credentials());
            long now = 0;
            for (var i = 0; i < ClientLimits.ReconnectDelaysMs.Length; i++)
            {
                _flow.OnDisconnected(DisconnectReason.ConnectFailed, "отказ");
                _reconnector.Update(now);
                Assert.That(_reconnector.WillRetry, Is.True);
                var delay = ClientLimits.ReconnectDelaysMs[i];

                _reconnector.Update(now + delay - 1);
                Assert.That(_connector.Endpoints.Count, Is.EqualTo(i + 1), "раньше паузы не подключаемся");
                _reconnector.Update(now + delay);
                Assert.That(_connector.Endpoints.Count, Is.EqualTo(i + 2));
                Assert.That(_flow.State, Is.EqualTo(LoginState.ConnectingLogin));
                now += delay;
            }

            _flow.OnDisconnected(DisconnectReason.ConnectFailed, "отказ");
            _reconnector.Update(now);
            _reconnector.Update(now + 100000);

            Assert.That(_reconnector.WillRetry, Is.False);
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.ConnectFailed));
            Assert.That(_connector.Endpoints.Count, Is.EqualTo(ClientLimits.ReconnectDelaysMs.Length + 1));
            Assert.That(_scheduled.ConvertAll(s => s.Delay), Is.EqualTo(ClientLimits.ReconnectDelaysMs));
        }

        [Test]
        public void DisconnectedInWorld_ReentersSameShardAndCharacter_AttemptsReset()
        {
            EnterWorld(characterSlot: 1);
            _reconnector.Update(0);
            _flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);
            _reconnector.Update(10);
            _sender.Calls.Clear();

            _reconnector.Update(10 + ClientLimits.ReconnectDelaysMs[0]);
            _flow.OnConnected();
            _flow.OnShardList(Shards());
            _reconnector.Update(2000);
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 9));
            _flow.OnConnected();
            _flow.Characters.OnCharacterList(Slots(), new StartingCity[0], 0);
            _reconnector.Update(2001);

            Assert.That(_sender.Calls, Is.EqualTo(new[] { "seed", "login", "shard", "game", "play" }));
            Assert.That(_sender.ShardIndex, Is.EqualTo(1), "тот же шард");
            Assert.That(_sender.CharacterSlot, Is.EqualTo(1), "тот же персонаж");
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.None));
            Assert.That(_reconnector.Attempts, Is.EqualTo(1));

            CompleteWorld();
            _reconnector.Update(3000);
            Assert.That(_reconnector.Attempts, Is.EqualTo(0), "снова в мире — счёт с нуля");
        }

        [TestCase(LoginErrorKind.LoginDenied)]
        [TestCase(LoginErrorKind.CharacterRejected)]
        [TestCase(LoginErrorKind.ProtocolError)]
        public void ServerRefusalsAndProtocolErrors_NotRetried(LoginErrorKind kind)
        {
            _flow.Start("127.0.0.1", 2593, Credentials());
            _flow.OnConnected();
            _flow.Progress.Fail(new LoginError(kind, 3, string.Empty));

            _reconnector.Update(0);
            _reconnector.Update(100000);

            Assert.That(_reconnector.WillRetry, Is.False);
            Assert.That(_connector.Endpoints.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectedBeforeWorld_NotRetried()
        {
            _flow.Start("127.0.0.1", 2593, Credentials());
            _flow.OnConnected();
            _flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);

            _reconnector.Update(0);
            _reconnector.Update(100000);

            Assert.That(_connector.Endpoints.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClosedByClient_NotRetried()
        {
            EnterWorld(characterSlot: 0);
            _reconnector.Update(0);
            _flow.OnDisconnected(DisconnectReason.Requested, string.Empty);

            _reconnector.Update(1);
            _reconnector.Update(100000);

            Assert.That(_flow.State, Is.EqualTo(LoginState.Closed));
            Assert.That(_reconnector.WillRetry, Is.False);
        }

        private void EnterWorld(int characterSlot)
        {
            _flow.Start("127.0.0.1", 2593, Credentials());
            _flow.OnConnected();
            _flow.OnShardList(Shards());
            _flow.SelectShard(1);
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            _flow.OnConnected();
            _flow.Characters.OnCharacterList(Slots(), new StartingCity[0], 0);
            _flow.Characters.Select(characterSlot);
            CompleteWorld();
        }

        private void CompleteWorld()
        {
            var world = new WorldEntry(_flow.Progress, new RecordingSessionSender());
            world.OnLoginConfirm(new PlayerState(new Serial(1), 0x190, Point3D.Zero, Direction.North, new MapSize(7168, 4096)));
            world.OnLoginComplete();
        }

        private static ShardEntry[] Shards() => new[]
        {
            new ShardEntry(0, "A", 0, 0, "127.0.0.1"),
            new ShardEntry(1, "B", 0, 0, "127.0.0.1"),
        };

        private static CharacterSlot[] Slots() => new[] { new CharacterSlot(0, "First"), new CharacterSlot(1, "AQuaruss") };

        private static LoginCredentials Credentials() => new LoginCredentials("player", "secret");
    }
}
