using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Параметры бота из командной строки (PLAN Стадия 1, п. 15): хост, порт, логин, персонаж, путь к файлу настроек.
    /// Пароль в командной строке не принимается: он остался бы в истории Терминала.
    /// </summary>
    public sealed class BotArgumentsTests
    {
        [Test]
        public void NoArguments_RunWithFileAndLog()
        {
            var parsed = BotArguments.Parse(new string[0]);

            Assert.That(parsed.Error, Is.Empty);
            Assert.That(parsed.Mode, Is.EqualTo(BotMode.Run));
            Assert.That(parsed.Overrides.Host, Is.Null);
            Assert.That(parsed.ConfigPath, Is.Null);
            Assert.That(parsed.PacketLog, Is.True);
        }

        [Test]
        public void Overrides_HostPortLoginCharacterConfig()
        {
            var parsed = BotArguments.Parse(new[]
            {
                "--host", "10.0.0.5", "--port", "2600", "--login", "gm", "--character", "Борис", "--config", "/tmp/bot.json",
                "--no-packet-log",
            });

            Assert.That(parsed.Error, Is.Empty);
            Assert.That(parsed.Overrides.Host, Is.EqualTo("10.0.0.5"));
            Assert.That(parsed.Overrides.Port, Is.EqualTo(2600));
            Assert.That(parsed.Overrides.Login, Is.EqualTo("gm"));
            Assert.That(parsed.Overrides.Character, Is.EqualTo("Борис"));
            Assert.That(parsed.ConfigPath, Is.EqualTo("/tmp/bot.json"));
            Assert.That(parsed.PacketLog, Is.False);
        }

        [Test]
        public void SelfCheckAndHelp_Modes()
        {
            Assert.That(BotArguments.Parse(new[] { "--self-check" }).Mode, Is.EqualTo(BotMode.SelfCheck));
            Assert.That(BotArguments.Parse(new[] { "--help" }).Mode, Is.EqualTo(BotMode.Help));
        }

        [Test]
        public void Password_Refused_ExplainsHistory()
        {
            var parsed = BotArguments.Parse(new[] { "--password", "s3cret" });

            Assert.That(parsed.Error, Does.Contain("истори").And.Contain("setup.sh"));
            Assert.That(parsed.Error, Does.Not.Contain("s3cret"));
        }

        [TestCase("--port", "abc")]
        [TestCase("--port", "0")]
        [TestCase("--port", "65536")]
        public void BadPort_Error(string name, string value)
        {
            Assert.That(BotArguments.Parse(new[] { name, value }).Error, Does.Contain("--port"));
        }

        [Test]
        public void MissingValue_Error()
        {
            Assert.That(BotArguments.Parse(new[] { "--host" }).Error, Does.Contain("--host"));
        }

        [Test]
        public void UnknownArgument_Error()
        {
            Assert.That(BotArguments.Parse(new[] { "--fast" }).Error, Does.Contain("--fast"));
        }
    }
}
