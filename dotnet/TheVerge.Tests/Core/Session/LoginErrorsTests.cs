using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Типизированные ошибки входа (PLAN Стадия 1, п. 11, «Реакция клиента на ошибки»). 0x53 сервер шлёт, когда другой
    /// персонаж аккаунта уже в мире (IncomingAccountPackets.cs, PlayCharacter и CreateCharacter: PMMessage.CharInWorld).
    /// Клиент не ждёт ответа сервера бесконечно. «Неверный клиент» сервер выгоняет через clientVerification.kickDelay
    /// = 20 с (UOContent/Misc/ClientVerification.cs) — об этом подсказка.
    /// </summary>
    public sealed class LoginErrorsTests
    {
        private RecordingAccountSender _sender = new RecordingAccountSender();
        private LoginFlow _flow = null!;
        private LoginWatchdog _watchdog = null!;

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingAccountSender();
            _flow = new LoginFlow(_sender, new RecordingConnector(), 1);
            _watchdog = new LoginWatchdog(_flow.Progress);
        }

        [Test]
        public void CharacterRejected_CharInWorld_TypedErrorWithCode()
        {
            ToCharacterSelection();
            _flow.Characters.Select(0);

            _flow.OnCharacterRejected(CharacterRejectReason.CharInWorld);

            Assert.That(_flow.State, Is.EqualTo(LoginState.Failed));
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.CharacterRejected));
            Assert.That(_flow.Error.Code, Is.EqualTo(5));
        }

        [Test]
        public void Watchdog_ServerSilentWhileAwaitingShardList_TimeoutAfterLimit()
        {
            _flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _watchdog.Update(1000);

            _watchdog.Update(1000 + ClientLimits.ServerResponseTimeoutMs - 1);
            Assert.That(_flow.State, Is.EqualTo(LoginState.AwaitingShardList));

            _watchdog.Update(1000 + ClientLimits.ServerResponseTimeoutMs);
            Assert.That(_flow.Error.Kind, Is.EqualTo(LoginErrorKind.Timeout));
        }

        [Test]
        public void Watchdog_ProgressResetsTimer()
        {
            _flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _watchdog.Update(0);
            _flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            _flow.SelectShard(0);
            _watchdog.Update(ClientLimits.ServerResponseTimeoutMs - 1);

            _watchdog.Update(2 * ClientLimits.ServerResponseTimeoutMs - 2);

            Assert.That(_flow.State, Is.EqualTo(LoginState.AwaitingRedirect));
        }

        [Test]
        public void Watchdog_HumanChoosing_NoTimeout()
        {
            ToCharacterSelection();
            _watchdog.Update(0);

            _watchdog.Update(10 * ClientLimits.ServerResponseTimeoutMs);

            Assert.That(_flow.State, Is.EqualTo(LoginState.CharacterSelection));
        }

        [Test]
        public void Watchdog_InWorld_NoTimeout()
        {
            ToCharacterSelection();
            _flow.Characters.Select(0);
            var world = new WorldEntry(_flow.Progress, new RecordingSessionSender());
            world.OnLoginConfirm(new PlayerState(new Serial(1), 0x190, Point3D.Zero, Direction.North, new MapSize(7168, 4096)));
            world.OnLoginComplete();
            _watchdog.Update(0);

            _watchdog.Update(10 * ClientLimits.ServerResponseTimeoutMs);

            Assert.That(_flow.State, Is.EqualTo(LoginState.InWorld));
        }

        [TestCase(5000, true)]
        [TestCase(24999, true)]
        [TestCase(25000, false)]
        public void Hint_ServerClosedSoonAfterEnteringWorld_MentionsClientVerification(long msInWorld, bool hinted)
        {
            var error = new LoginError(LoginErrorKind.Disconnected, (int)DisconnectReason.ServerClosed, string.Empty);

            var hint = DisconnectHint.For(error, msInWorld);

            Assert.That(hint.Contains("clientVerification") && hint.Contains("7.0.95.0"), Is.EqualTo(hinted));
        }

        [Test]
        public void Hint_OtherErrors_None()
        {
            Assert.That(DisconnectHint.For(new LoginError(LoginErrorKind.LoginDenied, 3, string.Empty), 1000), Is.Empty);
            Assert.That(DisconnectHint.For(new LoginError(LoginErrorKind.Disconnected, (int)DisconnectReason.ServerClosed, string.Empty), -1), Is.Empty,
                "не был в мире — не подсказка про проверку клиента");
        }

        private void ToCharacterSelection()
        {
            _flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            _flow.SelectShard(0);
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            _flow.OnConnected();
            _flow.Characters.OnCharacterList(new[] { new CharacterSlot(0, "AQuaruss") }, new StartingCity[0], 0);
        }
    }
}
