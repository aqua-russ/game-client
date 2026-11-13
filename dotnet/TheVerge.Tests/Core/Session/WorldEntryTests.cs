using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Tests.Core.Session
{
    /// <summary>
    /// Вход в мир (PLAN Стадия 1, п. 9). Сервер после выбора персонажа шлёт подтверждение входа, мир и конец загрузки
    /// (IncomingAccountPackets.cs, DoLogin: SendLoginConfirmation … SendLoginComplete); перед этим спрашивает версию
    /// (PlayCharacter → SendClientVersionRequest). Язык сервер пишет в Mobile.Language (Language), на дальность обзора
    /// отвечает 18 (SetUpdateRange).
    /// </summary>
    public sealed class WorldEntryTests
    {
        private RecordingAccountSender _account = new RecordingAccountSender();
        private RecordingSessionSender _session = new RecordingSessionSender();
        private LoginFlow _flow = null!;
        private WorldEntry _world = null!;

        [SetUp]
        public void SetUp()
        {
            _account = new RecordingAccountSender();
            _session = new RecordingSessionSender();
            _flow = new LoginFlow(_account, new RecordingConnector(), 1);
            _world = new WorldEntry(_flow.Progress, _session);
        }

        [Test]
        public void LoginConfirm_AfterCharacterChosen_PlayerStored_Loading()
        {
            ChooseCharacter();

            _world.OnLoginConfirm(Player());

            Assert.That(_flow.State, Is.EqualTo(LoginState.WorldLoading));
            Assert.That(_world.Player.Serial.Value, Is.EqualTo(0x3Du));
            Assert.That(_world.Player.Position, Is.EqualTo(new Point3D(633, 858, 0)));
            Assert.That(_session.Calls, Is.Empty);
        }

        [Test]
        public void LoginConfirm_BeforeCharacterChosen_Ignored()
        {
            _world.OnLoginConfirm(Player());

            Assert.That(_flow.State, Is.EqualTo(LoginState.Idle));
            Assert.That(_world.Player.Serial.IsValid, Is.False);
        }

        [Test]
        public void LoginComplete_InWorld_SendsLanguageAndViewRange()
        {
            ChooseCharacter();
            _world.OnLoginConfirm(Player());

            _world.OnLoginComplete();

            Assert.That(_flow.State, Is.EqualTo(LoginState.InWorld));
            Assert.That(_session.Calls, Is.EqualTo(new[] { "language ENU", "range 18" }));
        }

        [Test]
        public void LoginComplete_WithoutConfirm_Ignored()
        {
            ChooseCharacter();

            _world.OnLoginComplete();

            Assert.That(_flow.State, Is.EqualTo(LoginState.EnteringWorld));
            Assert.That(_session.Calls, Is.Empty);
        }

        [Test]
        public void VersionRequest_AnyState_AnswersTargetVersion()
        {
            var requests = new ServerRequests(_session);

            requests.OnClientVersionRequest();
            ChooseCharacter();
            requests.OnClientVersionRequest();

            Assert.That(_session.Calls, Is.EqualTo(new[] { "version 7.0.95.0", "version 7.0.95.0" }));
        }

        private void ChooseCharacter()
        {
            _flow.Start("127.0.0.1", 2593, new LoginCredentials("player", "secret"));
            _flow.OnConnected();
            _flow.OnShardList(new[] { new ShardEntry(0, "Shard", 0, 0, "127.0.0.1") });
            _flow.SelectShard(0);
            _flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 5));
            _flow.OnConnected();
            _flow.Characters.OnCharacterList(new[] { new CharacterSlot(0, "AQuaruss") }, new StartingCity[0], 0);
            _flow.Characters.Select(0);
        }

        private static PlayerState Player() =>
            new PlayerState(new Serial(0x3D), 0x190, new Point3D(633, 858, 0), Direction.South, new MapSize(7168, 4096));
    }
}
