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
        public void Idle_SecondsOrError()
        {
            Assert.That(BotArguments.Parse(new[] { "--idle", "35" }).IdleSeconds, Is.EqualTo(35));
            Assert.That(BotArguments.Parse(new string[0]).IdleSeconds, Is.EqualTo(0));
            Assert.That(BotArguments.Parse(new[] { "--idle", "-1" }).Error, Does.Contain("--idle"));
            Assert.That(BotArguments.Parse(new[] { "--idle", "abc" }).Error, Does.Contain("--idle"));
        }

        [Test]
        public void Action_RepeatedInOrder()
        {
            var parsed = BotArguments.Parse(new[] { "--action", "use:backpack", "--action", "use:backpack" });

            Assert.That(parsed.Error, Is.Empty);
            Assert.That(parsed.Actions.Count, Is.EqualTo(2));
            Assert.That(parsed.Actions[0].Kind, Is.EqualTo(BotActionKind.UseBackpack));
        }

        [Test]
        public void Action_SayAndAt()
        {
            var parsed = BotArguments.Parse(new[] { "--action", "say:[go 1500 1600", "--action", "at:1500,1600" });

            Assert.That(parsed.Error, Is.Empty);
            Assert.That(parsed.Actions[0].Kind, Is.EqualTo(BotActionKind.Say));
            Assert.That(parsed.Actions[0].Argument, Is.EqualTo("[go 1500 1600"));
            Assert.That(parsed.Actions[1].Kind, Is.EqualTo(BotActionKind.WaitAt));
        }

        [TestCase("say:")]
        [TestCase("say:   ")]
        [TestCase("at:1500")]
        [TestCase("at:x,y")]
        [TestCase("use")]
        [TestCase("use:sword")]
        [TestCase("fly:away")]
        [TestCase("")]
        public void Action_Unknown_Error(string text)
        {
            Assert.That(BotArguments.Parse(new[] { "--action", text }).Error, Does.Contain("--action"));
        }

        [Test]
        public void Reconnect_Flag()
        {
            Assert.That(BotArguments.Parse(new[] { "--reconnect" }).Reconnect, Is.True);
            Assert.That(BotArguments.Parse(new string[0]).Reconnect, Is.False);
        }

        [Test]
        public void CreateCharacter_Flag()
        {
            Assert.That(BotArguments.Parse(new[] { "--character", "Борис", "--create-character" }).CreateCharacter, Is.True);
            Assert.That(BotArguments.Parse(new string[0]).CreateCharacter, Is.False);
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
