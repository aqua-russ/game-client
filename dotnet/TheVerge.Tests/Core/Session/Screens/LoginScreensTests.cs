using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;

namespace TheVerge.Tests.Core.Session.Screens
{
    /// <summary>
    /// Экраны входа в Unity без Unity (PLAN Стадия 1, п. 16, критерий «Unity»): какой экран показан, что написано в строке
    /// статуса и куда ведёт каждая кнопка. Решения о протоколе — в LoginFlow; экраны только вызывают его.
    /// </summary>
    public sealed class LoginScreensTests
    {
        [Test]
        public void Start_LoginScreen_NoClientYet()
        {
            var h = new ScreensHarness();

            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Login));
            Assert.That(h.Clients, Is.Empty);
            Assert.DoesNotThrow(new System.Action(() => h.Tick()));
        }

        [Test]
        public void Submit_InvalidForm_NothingHappens()
        {
            var h = new ScreensHarness();

            h.Screens.Submit();

            Assert.That(h.Clients, Is.Empty);
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Login));
        }

        [Test]
        public void Submit_ConnectsToAddressFromForm_WaitingWithStatus()
        {
            var h = new ScreensHarness();
            h.Screens.Form.Address = "10.0.0.5:2600";

            h.SubmitLogin();

            Assert.That(h.Last.Connector.Endpoints, Is.EqualTo(new[] { "10.0.0.5:2600" }));
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Waiting));
            Assert.That(h.Screens.Status, Is.EqualTo("Подключение к серверу 10.0.0.5:2600…"));

            h.Last.Flow.OnConnected();
            h.Tick();
            Assert.That(h.Last.Account.Credentials.Username, Is.EqualTo("player"));
            Assert.That(h.Screens.Status, Is.EqualTo("Проверка логина и пароля…"));
        }

        [Test]
        public void OneShard_StillShownAsList_ChoiceByButton()
        {
            var h = new ScreensHarness();
            h.ToShards(1);

            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Shards));
            Assert.That(h.Screens.Shards.Count, Is.EqualTo(1));
            Assert.That(h.Last.Account.Calls, Does.Not.Contain("shard"), "сам шард не выбирается");

            h.Screens.ChooseShard(0);

            Assert.That(h.Last.Account.ShardIndex, Is.EqualTo(0));
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Waiting));
            Assert.That(h.Screens.Status, Is.EqualTo("Подключение к шарду…"));
        }

        [Test]
        public void ChooseShard_NotOnShardScreen_Ignored()
        {
            var h = new ScreensHarness();
            h.SubmitLogin();

            Assert.DoesNotThrow(new System.Action(() => h.Screens.ChooseShard(0)));
            Assert.That(h.Last.Account.Calls, Does.Not.Contain("shard"));
        }

        [Test]
        public void FullPath_LoginShardCharacterWorldLogout()
        {
            var h = new ScreensHarness();
            h.ToCharacters("Борис", string.Empty);
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Characters));

            h.Screens.Characters.Select(0);
            h.Screens.Characters.Enter();
            Assert.That(h.Last.Account.CharacterName, Is.EqualTo("Борис"));
            Assert.That(h.Screens.Status, Is.EqualTo("Вход в мир…"));

            h.EnterWorldOnServer();
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.InWorld));
            Assert.That(h.Screens.WorldStatus, Is.EqualTo("Борис · RTT — · в мире 00:00"));

            h.Screens.Logout();
            Assert.That(h.Last.Session.Calls, Does.Contain("logout"));
            Assert.That(h.Screens.Status, Is.EqualTo("Выход из игры…"));

            h.Last.Flow.OnDisconnected(DisconnectReason.Requested, string.Empty);
            h.Tick();
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Login));
            Assert.That(h.Screens.Message, Is.EqualTo("Вы вышли из игры."));
            Assert.That(h.Last.Disposed, Is.True, "соединение и поток клиента закрыты");
        }

        [Test]
        public void LoginDenied_BackToLoginWithReason_NewSubmitIsNewClient()
        {
            var h = new ScreensHarness();
            h.SubmitLogin();
            h.Last.Flow.OnConnected();
            h.Last.Flow.OnLoginRejected(LoginRejectReason.BadPassword);
            h.Tick();

            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Login));
            Assert.That(h.Screens.Message, Is.EqualTo("Сервер отказал во входе: неверный пароль."));
            Assert.That(h.Last.Disposed, Is.True);

            h.Screens.Form.Password = "right";
            h.Screens.Submit();
            Assert.That(h.Clients.Count, Is.EqualTo(2));
            Assert.That(h.Screens.Message, Is.Empty, "старая ошибка не висит над новым входом");
        }

        [Test]
        public void Cancel_WhileWaiting_ClosesClient_BackToLogin()
        {
            var h = new ScreensHarness();
            h.SubmitLogin();

            h.Screens.Cancel();

            Assert.That(h.Last.Disposed, Is.True);
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Login));
            Assert.That(h.Screens.Message, Is.EqualTo("Вход отменён."));
            Assert.DoesNotThrow(new System.Action(() => h.Tick()));
        }

        [Test]
        public void InWorldStatus_RttAndTime_TextChangesOncePerSecond()
        {
            var h = new ScreensHarness();
            h.ToWorld();
            var game = h.Last.Parts.Game;

            h.Tick(ClientLimits.PingIntervalMs);
            h.Clock.NowMs += 42;
            game.OnPingAck(1);
            h.Tick(0);
            Assert.That(h.Screens.WorldStatus, Is.EqualTo("Борис · RTT 42 мс · в мире 00:10"));

            var revision = h.Screens.Revision;
            h.Tick(100);
            Assert.That(h.Screens.Revision, Is.EqualTo(revision), "в пределах секунды экран не перерисовывается");

            h.Tick(3600 * 1000);
            Assert.That(h.Screens.WorldStatus, Does.EndWith("в мире 1:00:10"));
        }

        [Test]
        public void Dispose_ClosesClient()
        {
            var h = new ScreensHarness();
            h.SubmitLogin();

            h.Screens.Dispose();

            Assert.That(h.Last.Disposed, Is.True);
        }
    }
}
