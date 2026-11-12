using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Бот целиком, как его запускает человек или tools/check/all.sh (PLAN Стадия 1, п. 15): параметры, пароль из
    /// файла или скрытого ввода, лог пакетов в файл, итог и код выхода. Пароль никогда не попадает ни на экран,
    /// ни в лог. Самопроверка работает без сервера.
    /// </summary>
    // Права файла (chmod) есть только в macOS и Linux — там и работают бот и проверки.
    [UnsupportedOSPlatform("windows")]
    public sealed class BotAppTests
    {
        private const string Password = "s3cret-xyz";

        private string _root = string.Empty;
        private StringWriter _output = new StringWriter();
        private int _prompts;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "theverge-app-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
            _output = new StringWriter();
            _prompts = 0;
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void SelfCheck_WithoutServer_Ok()
        {
            var code = NewApp(null).Run(new[] { "--self-check" }, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.Success));
            Assert.That(_output.ToString(), Does.Contain("TheVerge.Bot: ОК"));
        }

        [Test]
        public void Help_ListsParameters()
        {
            var code = NewApp(null).Run(new[] { "--help" }, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.Success));
            Assert.That(_output.ToString(), Does.Contain("--login").And.Contain("setup.sh"));
        }

        [Test]
        public void BadArgument_Code1()
        {
            var code = NewApp(null).Run(new[] { "--fast" }, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain("--fast"));
        }

        [Test]
        public void NoPassword_NobodyToAsk_Code1_PointsToSetup()
        {
            var config = Config("{\"login\":\"gm\"}");

            var code = NewApp(null).Run(new[] { "--config", config }, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain("setup.sh"));
        }

        [Test]
        public void NoLogin_Code1()
        {
            var config = Config("{\"password\":\"" + Password + "\"}");

            var code = NewApp(null).Run(new[] { "--config", config }, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain("логин"));
        }

        [Test]
        public void NoClientFolder_NoConfig_Code1()
        {
            var app = new BotApp(_output, null, null);

            Assert.That(app.Run(new string[0], CancellationToken.None), Is.EqualTo(BotExitCode.BadArguments));
        }

        [Test]
        public void ServerRejects_LogFileWritten_PasswordNeverShown()
        {
            using (var server = new LocalServer())
            {
                var config = Config("{\"login\":\"gm\",\"password\":\"" + Password + "\"}");
                var answer = RejectLogin(server);

                var code = NewApp(null).Run(new[] { "--config", config, "--port", server.Port.ToString() }, CancellationToken.None);

                Assert.That(answer.Wait(5000), Is.True);
                Assert.That(code, Is.EqualTo(BotExitCode.LoginRejected));
                var logs = Directory.GetFiles(Path.Combine(_root, "Logs", "packets"));
                Assert.That(logs.Length, Is.EqualTo(1));
                var log = File.ReadAllText(logs[0]);
                Assert.That(log, Does.Contain("CONNECTED"));
                Assert.That(log, Does.Contain("0xEF").And.Contain("0x80").And.Contain("0x82"));
                Assert.That(_output.ToString(), Does.Contain(logs[0]).And.Contain("Код выхода 6"));
                Assert.That(_output.ToString() + log, Does.Not.Contain(Password));
            }
        }

        [Test]
        public void NoPasswordInFile_AsksOnce()
        {
            using (var server = new LocalServer())
            {
                var config = Config("{\"login\":\"gm\"}");
                var answer = RejectLogin(server);

                var code = NewApp(() => Password).Run(new[] { "--config", config, "--port", server.Port.ToString(), "--no-packet-log" },
                    CancellationToken.None);

                Assert.That(answer.Wait(5000), Is.True);
                Assert.That(code, Is.EqualTo(BotExitCode.LoginRejected));
                Assert.That(_prompts, Is.EqualTo(1));
                Assert.That(Directory.Exists(Path.Combine(_root, "Logs", "packets")), Is.False);
            }
        }

        [Test]
        public void LoginLongerThanServerField_Code1_NothingSent()
        {
            var config = Config("{\"login\":\"" + new string('a', 31) + "\",\"password\":\"" + Password + "\"}");

            var code = NewApp(null).Run(new[] { "--config", config, "--port", "1" }, CancellationToken.None);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain("30"));
        }

        [Test]
        public void PacketLogLimit_AsAgreed()
        {
            Assert.That(ClientLimits.PacketLogMaxChars, Is.EqualTo(64L * 1024 * 1024));
        }

        private BotApp NewApp(Func<string?>? prompt) =>
            new BotApp(_output, _root, prompt == null ? null : new Func<string?>(() =>
            {
                _prompts++;
                return prompt();
            }));

        // Сервер-заглушка: принимает подключение, читает seed и логин (21 + 62 байта, IncomingAccountPackets.cs),
        // отвечает отказом «неверный пароль» (0x82 0x03, OutgoingAccountPackets.cs) и закрывает соединение, как сервер.
        private static Task RejectLogin(LocalServer server) => Task.Run(() =>
        {
            server.Accept();
            server.ReadExactly(21 + 62);
            server.Client.Send(new byte[] { 0x82, 0x03 });
            server.CloseClient();
        });

        private string Config(string json)
        {
            var path = Path.Combine(_root, "bot.json");
            File.WriteAllText(path, json);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return path;
        }
    }
}
