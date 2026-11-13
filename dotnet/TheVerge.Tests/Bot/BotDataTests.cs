using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Tests.Data;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Папка файлов данных у бота (PLAN Стадия 2, п. 1): параметр --data. Что найдено — пишется при старте; папка не
    /// подходит — понятная ошибка и код 1 до подключения к серверу. Самопроверка с --data проверяет только папку.
    /// </summary>
    // Права файла (chmod) есть только в macOS и Linux — там и работают бот и проверки.
    [UnsupportedOSPlatform("windows")]
    public sealed class BotDataTests
    {
        private TempDataFolder _data = null!;
        private string _root = string.Empty;
        private StringWriter _output = new StringWriter();

        [SetUp]
        public void SetUp()
        {
            _data = new TempDataFolder();
            _data.Sized("map0.mul", 896L * 512 * 196);
            _data.Sized("staidx0.mul", 896L * 512 * 12);
            _data.Sized("statics0.mul", 0);
            _data.Sized("tiledata.mul", 1644544);
            _data.Sized("multi.idx", 0);
            _data.Sized("multi.mul", 0);
            _root = Path.Combine(Path.GetTempPath(), "theverge-botdata-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
            _output = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _data.Dispose();
            Directory.Delete(_root, true);
        }

        [Test]
        public void Parse_DataFolder()
        {
            Assert.That(BotArguments.Parse(new[] { "--data", "/uo" }).DataFolder, Is.EqualTo("/uo"));
            Assert.That(BotArguments.Parse(new string[0]).DataFolder, Is.Null);
            Assert.That(BotArguments.Parse(new[] { "--data" }).Error, Does.Contain("--data"));
        }

        [Test]
        public void SelfCheck_WithData_PrintsFormats()
        {
            var code = Run("--self-check", "--data", _data.Path);

            Assert.That(code, Is.EqualTo(BotExitCode.Success));
            Assert.That(_output.ToString(), Does.Contain("map0.mul").And.Contain("tiledata.mul").And.Contain("multi.idx"));
            Assert.That(_output.ToString(), Does.Contain("TheVerge.Bot: ОК"));
            Assert.That(_output.ToString(), Does.Contain("Tiledata прочитана: земли 0x4000, предметов 0x8000"));
            Assert.That(_output.ToString(), Does.Contain("Карта открыта: клетка (3584, 2048) — земля 0x0000, Z 0, статики: 0"));
        }

        [Test]
        public void SelfCheck_TileDataUnreadable_Code1()
        {
            // Файл есть и подходит по длине, но прочитать его нельзя — ошибка с именем файла, а не падение бота.
            File.SetUnixFileMode(_data.File("tiledata.mul"), UnixFileMode.None);

            var code = Run("--self-check", "--data", _data.Path);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain("tiledata.mul"));
        }

        [Test]
        public void SelfCheck_BadData_Code1()
        {
            _data.Delete("tiledata.mul");

            var code = Run("--self-check", "--data", _data.Path);

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain("tiledata.mul"));
            Assert.That(_output.ToString(), Does.Not.Contain("TheVerge.Bot: ОК"));
        }

        [Test]
        public void Run_BadData_Code1_BeforeConnecting()
        {
            var missing = Path.Combine(_root, "нет-папки");

            var code = Run("--data", missing, "--host", "127.0.0.1", "--port", "1");

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain(missing));
        }

        private int Run(params string[] args) => new BotApp(_output, _root, null).Run(args, CancellationToken.None);
    }
}
