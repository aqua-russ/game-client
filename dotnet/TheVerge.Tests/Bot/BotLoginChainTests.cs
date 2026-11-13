using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Session;
using TheVerge.Network.Transport;
using TheVerge.Tests.Network.Packets.Account;
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
            // Задачи 1.6–1.8: первый шард, вход на игровой сервер, персонаж по имени из настроек. Вход в мир — задача 1.9,
            // поэтому цель бота пока — отправленный выбор персонажа.
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

        private (FakeConnection Login, FakeConnection Game) LoginAndGame(string?[] characters)
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
                Packet(AccountServerSamples.CharacterList(characters, new[] { new AccountServerSamples.City("Britain", "Inn", 1, 2, 3, 0) }, 0)));
            return (login, game);
        }

        private BotRunner Runner(params IConnection[] connections)
        {
            var queue = new System.Collections.Generic.Queue<IConnection>(connections);
            return new BotRunner(() => queue.Dequeue(), new SystemClockForTests(), _log, null);
        }

        // Бот, который ждёт не того, не вешает тесты: через timeoutMs тест падает, а поток бота (фоновый) бросается.
        private static int RunWithin(BotRunner runner, BotLogin target, int timeoutMs)
        {
            var run = Task.Run(() => runner.Run(target, CancellationToken.None));
            Assert.That(run.Wait(timeoutMs), Is.True, "бот не закончил работу за " + timeoutMs + " мс");
            return run.Result;
        }

        private static InboundEvent Packet(byte[] bytes) => InboundEvent.FromPacket(bytes, bytes.Length, new CountingArrayPool());

        private static BotLogin Target(int port, string character = "", bool createCharacter = false) =>
            new BotLogin("127.0.0.1", port, new LoginCredentials("player", Password), character, createCharacter);

        private string LogText() => string.Join("\n", _log.Entries.Select(e => e.Level + ": " + e.Message));

        private string Infos() => string.Join("\n", _log.Entries.Where(e => e.Level == "info").Select(e => e.Message));

        private sealed class SystemClockForTests : TheVerge.Core.Ports.IClock
        {
            private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();

            public long NowMs => _watch.ElapsedMilliseconds;
        }
    }
}
