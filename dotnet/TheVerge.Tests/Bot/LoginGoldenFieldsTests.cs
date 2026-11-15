using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Bot.Replay;
using TheVerge.Core.Session;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Поля пакетов входа против эталона ClassicUO (PLAN Стадия 1, п. 12 и критерий «Сценарий вход против эталонного лога»;
    /// docs/REFERENCE-CLIENT.md §5, шаг 2). Бот проходит записанный вход (Bot/Replays/bot-login-modernuo.tsv, фейковый
    /// сервер), его пакеты сравниваются с golden/01-login.tsv байт в байт. Отличаться могут только поля из маски — у каждого
    /// причина по исходнику сервера (Projects/UOContent/Network/Packets/IncomingAccountPackets.cs и др.) или по решению
    /// (golden/allowed-diffs.md).
    /// </summary>
    public sealed class LoginGoldenFieldsTests
    {
        private static readonly string Golden = Path.Combine(AppContext.BaseDirectory, "Golden", "01-login.tsv");
        private static readonly string Recorded = Path.Combine(AppContext.BaseDirectory, "Bot", "Replays", "bot-login-modernuo.tsv");

        private const int None = -1;

        // Что законно отличается от ClassicUO. Номера байтов — от начала пакета (с номером пакета).
        internal static GoldenFieldComparison LoginRules() => new GoldenFieldComparison()
            .Allow(0xEF, None, 1, 4, "seed: любое число, кроме 0 (LoginServerSeed); у ClassicUO — локальный IP, у нас — случайное, умолчание 1.5")
            .Allow(0x80, None, 1, 30, "логин — другой аккаунт (AccountLogin: ReadLatin1Safe(30))")
            .Allow(0x80, None, 31, 60, "пароль (в логах скрыт)")
            .Allow(0x80, None, 61, 61, "последний байт сервер не читает (AccountLogin читает только логин и пароль); умолчание 1.5 — 0")
            .Allow(0x91, None, 1, 4, "ключ входа — тот, что сервер прислал в 0x8C этому соединению (GameLogin: authId)")
            .Allow(0x91, None, 5, 34, "логин — другой аккаунт")
            .Allow(0x91, None, 35, 64, "пароль (в логах скрыт)")
            .Allow(0x5D, None, 5, 34, "имя персонажа — другой персонаж; сервер его пропускает (PlayCharacter: Seek(36))")
            .Allow(0x5D, None, 37, 40, "флаги карт: ClassicUO — все карты 0x3F, у нас только Фелукка 0x01 (ClientMapFlags, TARGET.md §1)")
            .Allow(0x5D, None, 69, 72, "IP клиента сервер не читает (PlayCharacter: Seek(4)); ClassicUO пишет 127.0.0.1, мы — 0")
            .Allow(0xBF, 0x0B, 5, 8, "язык клиента: ClassicUO — IVL, у нас ENU (умолчание 1.9, ClientLimits.Language)")
            .Allow(0xC8, None, 1, 1, "дальность обзора: ClassicUO просит 24, мы 18; сервер всегда отвечает 18 (SetUpdateRange)")
            .AllowBotOnly(0xD1, None);

        [Test]
        public void LoginPackets_SameFieldsAsClassicUo()
        {
            var bot = ClientPackets(RunBotThroughRecordedLogin());
            var golden = ClientPackets(File.ReadAllText(Golden));
            Assert.That(bot.Count, Is.GreaterThanOrEqualTo(8), "бот прошёл вход");

            var problems = LoginRules().Compare(bot, golden);

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Comparator_NamesChangedField()
        {
            var golden = ClientPackets(File.ReadAllText(Golden));
            var seed = (byte[])golden[0].Clone();
            seed[8] = 0x06; // версия клиента 7.0.95.0 → 7.0.6…: сервер по ней узнаёт клиента (LoginServerSeed)

            var problems = LoginRules().Compare(new[] { seed }, golden);

            Assert.That(problems, Is.EqualTo(new[] { "0xEF: байт 8 — 0x06, у ClassicUO 0x07." }));
        }

        private static string RunBotThroughRecordedLogin()
        {
            var server = new ReplayServer(ReplayScript.Read(new StringReader(File.ReadAllText(Recorded))));
            var clock = new SystemClock();
            var log = new RecordingLog();
            var text = new StringWriter();
            var packetLog = new PacketLogWriter(text, clock, log, ClientLimits.PacketLogMaxChars);
            var runner = new BotRunner(server.NextConnection, clock, log, packetLog);
            var login = new BotLogin("127.0.0.1", 2593, new LoginCredentials("replaybot", "any"), "Replay");
            var run = Task.Run(() => runner.Run(login, CancellationToken.None));
            Assert.That(run.Wait(10000), Is.True, "бот не закончил работу");
            Assert.That(server.Mismatches, Is.Empty);
            return text.ToString();
        }

        private static List<byte[]> ClientPackets(string log)
        {
            var result = new List<byte[]>();
            var reader = new PacketLogReader(new StringReader(log));
            while (reader.TryRead(out var entry))
            {
                if (entry.Kind == PacketLogKind.Packet && entry.Direction == PacketDirection.ClientToServer)
                {
                    result.Add(entry.Packet);
                }
            }

            return result;
        }

        private sealed class SystemClock : TheVerge.Core.Ports.IClock
        {
            private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();

            public long NowMs => _watch.ElapsedMilliseconds;
        }
    }
}
