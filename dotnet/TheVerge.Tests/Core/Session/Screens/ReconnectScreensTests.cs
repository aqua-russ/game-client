using NUnit.Framework;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;

namespace TheVerge.Tests.Core.Session.Screens
{
    /// <summary>
    /// Повтор входа в Unity включён (ADR 0016 п. 6): экран показывает «переподключение, попытка N из 5» вместо ошибки,
    /// пока Reconnector ещё попробует; попытки кончились — экран входа с причиной.
    /// </summary>
    public sealed class ReconnectScreensTests
    {
        private static int Max => ClientLimits.ReconnectDelaysMs.Length;

        [Test]
        public void DisconnectInWorld_ReconnectStatus_ThenBackInWorld()
        {
            var h = new ScreensHarness();
            h.ToWorld();

            h.Last.Flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);
            h.Tick(60000);
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Waiting));
            Assert.That(h.Screens.Status, Is.EqualTo("Связь потеряна. Переподключение, попытка 1 из " + Max + " через 1 с…"));
            Assert.That(h.Last.Disposed, Is.False, "клиент тот же: повторяет Reconnector");

            h.Tick(ClientLimits.ReconnectDelaysMs[0]);
            Assert.That(h.Screens.Status, Is.EqualTo("Переподключение, попытка 1 из " + Max + ": подключение к серверу 127.0.0.1:2593…"));

            h.Last.Flow.OnConnected();
            h.Last.Flow.OnShardList(new[] { new ShardEntry(0, "Shard 1", 0, 0, "127.0.0.1") });
            h.Tick();
            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Waiting), "тот же шард выбран сам — список не показывается");

            h.Last.Flow.OnRedirect(new ServerRedirect("127.0.0.1", 2593, 8));
            h.Last.Flow.OnConnected();
            h.Last.Flow.Characters.OnCharacterList(new[] { new CharacterSlot(0, "Борис") }, new StartingCity[0], 0);
            h.Tick();
            h.EnterWorldOnServer();

            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.InWorld));
            Assert.That(h.Screens.Status, Is.Empty);
        }

        [Test]
        public void ServerDown_RetriesShown_ThenLoginScreenWithReason()
        {
            var h = new ScreensHarness();
            h.SubmitLogin();

            for (var attempt = 1; attempt <= Max; attempt++)
            {
                h.Last.Flow.OnDisconnected(DisconnectReason.ConnectFailed, "refused");
                h.Tick();
                Assert.That(h.Screens.Status, Does.Contain("попытка " + attempt + " из " + Max), "попытка " + attempt);
                h.Tick(ClientLimits.ReconnectDelaysMs[attempt - 1]);
            }

            h.Last.Flow.OnDisconnected(DisconnectReason.ConnectFailed, "refused");
            h.Tick();

            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Login));
            Assert.That(h.Screens.Message, Does.StartWith("Не удалось подключиться к серверу"));
            Assert.That(h.Last.Disposed, Is.True);
        }

        [Test]
        public void Cancel_DuringReconnectPause_StopsRetrying()
        {
            var h = new ScreensHarness();
            h.ToWorld();
            h.Last.Flow.OnDisconnected(DisconnectReason.ServerClosed, string.Empty);
            h.Tick();

            h.Screens.Cancel();
            h.Tick(100000);

            Assert.That(h.Screens.Screen, Is.EqualTo(LoginScreen.Login));
            Assert.That(h.Last.Disposed, Is.True);
            Assert.That(h.Last.Connector.Endpoints.Count, Is.EqualTo(2), "после отмены новых подключений нет");
        }
    }
}
