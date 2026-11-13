using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Packets.Account;
using TheVerge.Tests.Network.Packets.Session;
using TheVerge.Tests.Network.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Бот проходит цепочку входа (PLAN Стадия 1, пп. 6–8) на диалоге с сервером в памяти (FakeConnection.Reply): первый
    /// шард, вход на игровой сервер по редиректу, персонаж по имени, первый или созданный. Пакеты сервера —
    /// AccountServerSamples (по коду OutgoingAccountPackets.cs).
    /// </summary>
    public sealed class BotLoginChainTests
    {
        private const string Password = "s3cretPw";

        private RecordingLog _log = new RecordingLog();

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLog();
        }

        [Test]
        public void FirstShard_GameServer_CharacterChosenByName_Success()
        {
            // Задачи 1.6–1.8: первый шард, вход на игровой сервер, персонаж по имени из настроек.
            var (login, game) = LoginAndGame(new[] { "AQua", null, "Борис", null, null });

            var code = RunWithin(Runner(login, game), Target(2593, "борис"), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
            Assert.That(login.Sent[1], Is.EqualTo(new byte[] { 0xA0, 0x00, 0x00 }));
            Assert.That(game.Sent[0].Length, Is.EqualTo(4 + 65));
            Assert.That(game.Sent[1][0], Is.EqualTo(0x5D));
            Assert.That(game.Sent[1][1 + 4 + 30 + 2 + 4 + 24 + 3], Is.EqualTo(2), "слот Бориса");
            Assert.That(Infos(), Does.Contain("Remaster UO").And.Contain("127.0.0.1:2593").And.Contain("Борис"));
            Assert.That(_log.Errors, Is.Empty);
        }

        [Test]
        public void CharacterChosen_EntersWorld_AnswersVersion_SendsLanguageAndRange()
        {
            // Задача 1.9: вход в мир.
            var (login, game) = LoginAndGame(new[] { "AQuaruss", null, null, null, null });

            var code = RunWithin(Runner(login, game), Target(2593, "AQuaruss"), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
            var afterChoice = game.Sent.Skip(2).SelectMany(b => b).ToArray();
            Assert.That(afterChoice[0], Is.EqualTo(0xBD), "ответ на запрос версии");
            var languageAndRange = new byte[] { 0xBF, 0x00, 0x09, 0x00, 0x0B, (byte)'E', (byte)'N', (byte)'U', 0x00, 0xC8, 18 };
            Assert.That(Contains(afterChoice, languageAndRange), Is.True, "язык и сразу за ним дальность обзора");
            Assert.That(Infos(), Does.Contain("в мире").And.Contain("633"));
        }

        [Test]
        public void InWorld_IdleZero_LogsOutAtOnce_ServerAck_Success()
        {
            // Задача 1.10: в мире бот ждёт --idle секунд (здесь 0) и выходит: 0xD1 → 0xD1 0x01 → соединение закрыл бот.
            var (login, game) = LoginAndGame(new[] { "AQuaruss", null, null, null, null });

            var code = RunWithin(Runner(login, game), Target(2593, "AQuaruss"), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
            Assert.That(game.Sent.Last(), Is.EqualTo(new byte[] { 0xD1, 0x00 }));
            Assert.That(Infos(), Does.Contain("Вышел из игры"));
            Assert.That(_log.Errors, Is.Empty);
        }

        [Test]
        public void InWorld_IdleOneSecond_LogoutNotBefore()
        {
            var (login, game) = LoginAndGame(new[] { "AQuaruss", null, null, null, null });
            var watch = System.Diagnostics.Stopwatch.StartNew();

            var code = RunWithin(Runner(login, game), Target(2593, "AQuaruss", idleSeconds: 1), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(1000));
        }

        [Test]
        public void CharacterRejected_CharInWorld_Code6_ReasonInWords()
        {
            var (login, game) = LoginAndGame(new[] { "AQuaruss", null, null, null, null }, rejectCharacter: true);

            var code = RunWithin(Runner(login, game), Target(2593, "AQuaruss"), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.LoginRejected), LogText());
            Assert.That(_log.Errors.Single(), Does.Contain("уже в мире"));
        }

        [Test]
        public void ServerSilentAfterLogin_TimeoutError_NotHang()
        {
            var login = new FakeConnection();
            login.Push(InboundEvent.Connected());
            var queue = new System.Collections.Generic.Queue<IConnection>(new IConnection[] { login });
            var runner = new BotRunner(() => queue.Dequeue(), new BotTestClocks.Stepping(1000), _log, null);

            var code = RunWithin(runner, Target(2593), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectionLost), LogText());
            Assert.That(_log.Errors.Single(), Does.Contain("не ответил"));
        }

        [Test]
        public void DropInWorld_Reconnect_SameCharacterAgain_ThenLogout()
        {
            // Задача 1.11б: с --reconnect разрыв в мире — повтор через ClientLimits.ReconnectDelaysMs[0] тем же персонажем.
            // Бот стоит в мире (--idle 1): разрыв после запрошенного выхода — это выход, а не повод для повтора.
            var (login1, game1) = LoginAndGame(new[] { "First", "AQuaruss", null, null, null }, dropInWorld: true);
            var (login2, game2) = LoginAndGame(new[] { "First", "AQuaruss", null, null, null });

            var code = RunWithin(Runner(login1, game1, login2, game2), Target(2593, "AQuaruss", idleSeconds: 1, reconnect: true), 8000);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
            Assert.That(game2.Sent[1][0], Is.EqualTo(0x5D));
            Assert.That(game2.Sent[1][1 + 4 + 30 + 2 + 4 + 24 + 3], Is.EqualTo(1), "тот же персонаж");
            Assert.That(Infos(), Does.Contain("Повтор входа через 1 с"));
            Assert.That(game2.Sent.Last(), Is.EqualTo(new byte[] { 0xD1, 0x00 }));
        }

        [Test]
        public void DropInWorld_Reconnect_CharacterGone_BotSaysSo_NotHang()
        {
            // После повтора входа прежнего персонажа нет в списке: бот не ждёт молча, а называет причину.
            var (login1, game1) = LoginAndGame(new[] { "First", "AQuaruss", null, null, null }, dropInWorld: true);
            var (login2, game2) = LoginAndGame(new[] { "First", null, null, null, null });

            var code = RunWithin(Runner(login1, game1, login2, game2), Target(2593, "AQuaruss", idleSeconds: 1, reconnect: true), 8000);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments), LogText());
            Assert.That(_log.Errors.Last(), Does.Contain("AQuaruss"));
        }

        [Test]
        public void CreateCharacter_ServerSentNoCities_ProblemInWords_NotCrash()
        {
            var (login, game) = LoginAndGame(new[] { null, null, null, null, (string?)null }, noCities: true);

            var code = RunWithin(Runner(login, game), Target(2593, "Botik", createCharacter: true), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments), LogText());
            Assert.That(_log.Errors.Single(), Does.Contain("город"));
        }

        [Test]
        public void DropInWorld_NoReconnectFlag_ConnectionLost()
        {
            var (login, game) = LoginAndGame(new[] { "AQuaruss", null, null, null, null }, dropInWorld: true);

            var code = RunWithin(Runner(login, game), Target(2593, "AQuaruss", idleSeconds: 60), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.ConnectionLost), LogText());
        }

        [Test]
        public void NoCharacterName_FirstOccupiedSlotChosen()
        {
            var (login, game) = LoginAndGame(new[] { null, "AQua", null, null, null });

            var code = RunWithin(Runner(login, game), Target(2593, string.Empty), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
            Assert.That(game.Sent[1][1 + 4 + 30 + 2 + 4 + 24 + 3], Is.EqualTo(1));
        }

        [Test]
        public void CharacterNotFound_NoCreateFlag_BadArgumentsInWords_NothingSent()
        {
            var (login, game) = LoginAndGame(new[] { "AQua", null, null, null, null });

            var code = RunWithin(Runner(login, game), Target(2593, "Nobody"), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments), LogText());
            Assert.That(_log.Errors.Single(), Does.Contain("Nobody").And.Contain("--create-character"));
            Assert.That(game.Sent.Count, Is.EqualTo(1), "ни выбора, ни создания");
        }

        [Test]
        public void CharacterNotFound_CreateFlag_CreatesInFirstFreeSlot()
        {
            var (login, game) = LoginAndGame(new[] { "AQua", null, null, null, null });

            var code = RunWithin(Runner(login, game), Target(2593, "Botik", createCharacter: true), 5000);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), LogText());
            Assert.That(game.Sent[1][0], Is.EqualTo(0xF8));
            Assert.That(game.Sent[1].Length, Is.EqualTo(106));
            Assert.That(Infos(), Does.Contain("Botik"));
        }

        private static bool Contains(byte[] data, byte[] part)
        {
            for (var i = 0; i + part.Length <= data.Length; i++)
            {
                if (data.Skip(i).Take(part.Length).SequenceEqual(part))
                {
                    return true;
                }
            }

            return false;
        }

        private (FakeConnection Login, FakeConnection Game) LoginAndGame(string?[] characters, bool rejectCharacter = false, bool dropInWorld = false, bool noCities = false)
        {
            var localhost = new byte[] { 127, 0, 0, 1 };
            var login = new FakeConnection();
            login.Push(InboundEvent.Connected());
            login.Reply(Packet(AccountServerSamples.ServerList(("Remaster UO", 0, 3, localhost))));
            login.Reply(
                Packet(AccountServerSamples.PlayServerAck(localhost, 2593, 77)),
                InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty));
            var game = new FakeConnection();
            game.Push(InboundEvent.Connected());
            game.Reply(
                Packet(AccountServerSamples.SupportedFeatures(0x8000_02DF)),
                Packet(AccountServerSamples.CharacterList(characters,
                    noCities ? new AccountServerSamples.City[0] : new[] { new AccountServerSamples.City("Britain", "Inn", 1, 2, 3, 0) }, 0)));
            if (rejectCharacter)
            {
                game.Reply(Packet(new byte[] { 0xBD, 0x00, 0x03 }), Packet(new byte[] { 0x53, 0x05 }));
                return (login, game);
            }

            // На выбор или создание персонажа: запрос версии, подтверждение входа, пакет мира без обработчика, конец загрузки.
            game.Reply(
                Packet(new byte[] { 0xBD, 0x00, 0x03 }),
                Packet(WorldSamples.LoginConfirm(0x3D, 633, 858, 0)),
                Packet(new byte[] { 0x4F, 0x10 }),
                Packet(new byte[] { 0x55 }));
            if (dropInWorld)
            {
                game.Reply(InboundEvent.Closed(ConnectionCloseReason.ServerClosed, string.Empty)); // разрыв сразу в мире
                return (login, game);
            }

            game.Reply(); // ответ на версию, язык и дальность — ничего
            game.Reply(Packet(new byte[] { 0xD1, 0x01 })); // на выход — разрешение (SendLogoutAck)
            return (login, game);
        }

        private BotRunner Runner(params IConnection[] connections)
        {
            var queue = new System.Collections.Generic.Queue<IConnection>(connections);
            return new BotRunner(() => queue.Dequeue(), new BotTestClocks.Real(), _log, null);
        }

        // Бот, который ждёт не того, не вешает тесты: через timeoutMs тест падает, а поток бота (фоновый) бросается.
        private static int RunWithin(BotRunner runner, BotLogin target, int timeoutMs)
        {
            var run = Task.Run(() => runner.Run(target, CancellationToken.None));
            Assert.That(run.Wait(timeoutMs), Is.True, "бот не закончил работу за " + timeoutMs + " мс");
            return run.Result;
        }

        private static InboundEvent Packet(byte[] bytes) => InboundEvent.FromPacket(bytes, bytes.Length, new CountingArrayPool());

        private static BotLogin Target(int port, string character = "", bool createCharacter = false, int idleSeconds = 0, bool reconnect = false) =>
            new BotLogin("127.0.0.1", port, new LoginCredentials("player", Password), character, createCharacter)
            {
                IdleSeconds = idleSeconds,
                Reconnect = reconnect,
            };

        private string LogText() => string.Join("\n", _log.Entries.Select(e => e.Level + ": " + e.Message));

        private string Infos() => string.Join("\n", _log.Entries.Where(e => e.Level == "info").Select(e => e.Message));
    }
}
