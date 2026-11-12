using System.IO;
using System.Runtime.Versioning;
using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Файл настроек бота с паролем (remaster/secrets/bot.json, вне обоих git-репозиториев). Чего нет в файле — берётся
    /// по умолчанию: сервер стенда 127.0.0.1:2593 (docs/TARGET.md §5, modernuo.json listeners). Параметры командной строки
    /// главнее файла. Файл, который могут читать другие пользователи компьютера, — предупреждение.
    /// </summary>
    // Права файла (chmod) есть только в macOS и Linux — там и работают бот и проверки.
    [UnsupportedOSPlatform("windows")]
    public sealed class BotConfigFileTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "theverge-bot-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_dir, true);

        [Test]
        public void AllFields_Read()
        {
            var path = Write("{\"host\":\"10.0.0.5\",\"port\":2600,\"login\":\"gm\",\"password\":\"s3cret\",\"character\":\"Борис\"}");

            var loaded = BotConfigFile.Load(path, new BotOverrides());

            Assert.That(loaded.Error, Is.Empty);
            Assert.That(loaded.Settings.Host, Is.EqualTo("10.0.0.5"));
            Assert.That(loaded.Settings.Port, Is.EqualTo(2600));
            Assert.That(loaded.Settings.Login, Is.EqualTo("gm"));
            Assert.That(loaded.Settings.Password, Is.EqualTo("s3cret"));
            Assert.That(loaded.Settings.Character, Is.EqualTo("Борис"));
        }

        [Test]
        public void MissingFields_StandDefaults()
        {
            var loaded = BotConfigFile.Load(Write("{\"login\":\"gm\",\"password\":\"p\"}"), new BotOverrides());

            Assert.That(loaded.Settings.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(loaded.Settings.Port, Is.EqualTo(2593));
            Assert.That(loaded.Settings.Character, Is.Empty);
        }

        [Test]
        public void CommandLine_WinsOverFile()
        {
            var path = Write("{\"host\":\"10.0.0.5\",\"port\":2600,\"login\":\"gm\",\"password\":\"p\",\"character\":\"A\"}");

            var loaded = BotConfigFile.Load(path, new BotOverrides { Host = "127.0.0.1", Port = 2593, Login = "player", Character = "B" });

            Assert.That(loaded.Settings.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(loaded.Settings.Port, Is.EqualTo(2593));
            Assert.That(loaded.Settings.Login, Is.EqualTo("player"));
            Assert.That(loaded.Settings.Character, Is.EqualTo("B"));
            Assert.That(loaded.Settings.Password, Is.EqualTo("p"));
        }

        [Test]
        public void NoFile_NoPassword_DefaultsStillApply()
        {
            var loaded = BotConfigFile.Load(Path.Combine(_dir, "нет.json"), new BotOverrides { Login = "gm" });

            Assert.That(loaded.Error, Is.Empty);
            Assert.That(loaded.Settings.Password, Is.Empty);
            Assert.That(loaded.Settings.Login, Is.EqualTo("gm"));
        }

        [TestCase("{broken")]
        [TestCase("{\"port\":\"много\"}")]
        [TestCase("[1,2]")]
        public void BrokenFile_ErrorWithPath_NoContent(string text)
        {
            var path = Write(text);

            var loaded = BotConfigFile.Load(path, new BotOverrides());

            Assert.That(loaded.Error, Does.Contain(path));
        }

        [Test]
        public void ReadableByOthers_Warning()
        {
            var path = Write("{\"login\":\"gm\",\"password\":\"p\"}");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            Assert.That(BotConfigFile.Load(path, new BotOverrides()).Warning, Does.Contain("chmod 600"));
        }

        [Test]
        public void OwnerOnly_NoWarning()
        {
            var path = Write("{\"login\":\"gm\",\"password\":\"p\"}");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            Assert.That(BotConfigFile.Load(path, new BotOverrides()).Warning, Is.Empty);
        }

        private string Write(string text)
        {
            var path = Path.Combine(_dir, "bot.json");
            File.WriteAllText(path, text);
            return path;
        }
    }
}
