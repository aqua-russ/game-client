using System.IO;
using System.Threading;
using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Бот переводит запись посредника в лог пакетов: TheVerge.Bot --convert-capture &lt;запись&gt; &lt;лог&gt; (PLAN Стадия 0,
    /// п. 6; tools/golden/record.sh). Итог — строкой; что не разобрано — списком; код выхода 4 (ошибка протокола).
    /// </summary>
    public sealed class BotCaptureCommandTests
    {
        private string _root = string.Empty;
        private StringWriter _output = new StringWriter();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "theverge-capture-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
            _output = new StringWriter();
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void Convert_WritesLogAndSummary()
        {
            var capture = Write("ok.capture", CaptureConverter.FormatLine + "\n0\t1\topen\n5\t1\tc2s\t" + SeedHex + "\n6\t1\ts2c\t8c7f0000010a21e6a23f31\n7\t1\tclose\ts\n");
            var log = Path.Combine(_root, "ok.tsv");

            var code = Run("--convert-capture", capture, log);

            Assert.That(code, Is.EqualTo(BotExitCode.Success), _output.ToString());
            Assert.That(File.ReadAllText(log), Does.Contain("LoginSeed").And.Contain("ServerRedirect"));
            Assert.That(_output.ToString(), Does.Contain("Запись переведена: соединений 1, пакетов клиента 1, сервера 1, ошибок нет"));
        }

        [Test]
        public void Convert_Errors_ListedCode4()
        {
            var capture = Write("bad.capture", CaptureConverter.FormatLine + "\n0\t1\topen\n5\t1\tc2s\t" + SeedHex + "fe0102\n");

            var code = Run("--convert-capture", capture, Path.Combine(_root, "bad.tsv"));

            Assert.That(code, Is.EqualTo(BotExitCode.ProtocolError));
            Assert.That(_output.ToString(), Does.Contain("ошибок 1").And.Contain("0xFE"));
        }

        [Test]
        public void Convert_NoFile_Code1()
        {
            var code = Run("--convert-capture", Path.Combine(_root, "none.capture"), Path.Combine(_root, "x.tsv"));

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
            Assert.That(_output.ToString(), Does.Contain("нет файла"));
        }

        [Test]
        public void Convert_NeedsTwoPaths()
        {
            var code = Run("--convert-capture", "only-one");

            Assert.That(code, Is.EqualTo(BotExitCode.BadArguments));
        }

        private const string SeedHex = "ef0102030400000007000000000000005f00000000";

        private int Run(params string[] args) => new BotApp(_output, null, null).Run(args, CancellationToken.None);

        private string Write(string name, string text)
        {
            var path = Path.Combine(_root, name);
            File.WriteAllText(path, text);
            return path;
        }
    }
}
