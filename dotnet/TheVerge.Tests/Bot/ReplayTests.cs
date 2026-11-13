using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Bot.Replay;
using TheVerge.Core.Session;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Фейковый сервер для CI (PLAN Стадия 1, п. 13): бот проходит записанный вход на настоящий ModernUO без сервера.
    /// Запись — Bot/Replays/bot-login-modernuo.tsv: tools/bot/run.sh против копии сервера 2026-11-13, аккаунт replaybot,
    /// персонаж Replay; пароль в записи скрыт логгером.
    /// </summary>
    public sealed class ReplayTests
    {
        private static readonly string Recorded = Path.Combine(AppContext.BaseDirectory, "Bot", "Replays", "bot-login-modernuo.tsv");

        private RecordingLog _log = new RecordingLog();

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLog();
        }

        [Test]
        public void RecordedLogin_BotPassesWithoutServer_EveryClientPacketInOrder()
        {
            var server = new ReplayServer(ReplayScript.Read(new StringReader(File.ReadAllText(Recorded))));

            var code = Run(server, "Replay");

            Assert.That(server.Mismatches, Is.Empty);
            Assert.That(code, Is.EqualTo(BotExitCode.Success), Text());
            Assert.That(server.IsComplete, Is.True, "все пакеты клиента из записи пришли");
            Assert.That(Text(), Does.Contain("Персонаж в мире").And.Contain("Вышел из игры"));
        }

        [Test]
        public void Script_TwoConnections_LoginThenGame()
        {
            var script = ReplayScript.Read(new StringReader(File.ReadAllText(Recorded)));

            Assert.That(script.Connections.Count, Is.EqualTo(2));
            Assert.That(script.Connections[1].Any(e => e.Kind == TheVerge.Network.Diagnostics.PacketLogKind.Packet && e.Packet[0] == 0x1B), Is.True);
        }

        [Test]
        public void ClientSendsOtherPacket_MismatchNamed_BotDoesNotHang()
        {
            // Запись, где после списка шардов сервер ждёт не выбор шарда 0xA0, а 0xD1.
            var text = File.ReadAllText(Recorded).Replace("C>S\t0xA0\t-\tSelectServer\t3\t-\tA0 00 00", "C>S\t0xD1\t-\tLogout\t2\t-\tD1 00");
            var server = new ReplayServer(ReplayScript.Read(new StringReader(text)));

            var code = Run(server, "Replay");

            Assert.That(code, Is.Not.EqualTo(BotExitCode.Success));
            Assert.That(server.Mismatches.Single(), Does.Contain("0xD1").And.Contain("0xA0"));
        }

        private int Run(ReplayServer server, string character)
        {
            var runner = new BotRunner(server.NextConnection, new SystemClock(), _log, null);
            var login = new BotLogin("127.0.0.1", 2593, new LoginCredentials("replaybot", "any"), character);
            var run = Task.Run(() => runner.Run(login, CancellationToken.None));
            Assert.That(run.Wait(10000), Is.True, "бот не закончил работу");
            return run.Result;
        }

        private string Text() => string.Join("\n", _log.Entries.Select(e => e.Level + ": " + e.Message));

        private sealed class SystemClock : TheVerge.Core.Ports.IClock
        {
            private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();

            public long NowMs => _watch.ElapsedMilliseconds;
        }
    }
}
