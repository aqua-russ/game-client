using System.IO;
using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>Лог бота в консоль: строка на запись, время и уровень по-русски, чтобы человек сразу видел ошибку.</summary>
    public sealed class ConsoleLogTests
    {
        [Test]
        public void Levels_OneLineEach_WithTimeAndLevel()
        {
            var output = new StringWriter();
            var log = new ConsoleLog(output);

            log.Info("подключено");
            log.Warning("пакет пропущен");
            log.Error("сбой");

            var lines = output.ToString().TrimEnd('\n').Split('\n');
            Assert.That(lines.Length, Is.EqualTo(3));
            Assert.That(lines[0], Does.Match(@"^\d\d:\d\d:\d\d\.\d\d\d ИНФО подключено$"));
            Assert.That(lines[1], Does.Match(@"^\d\d:\d\d:\d\d\.\d\d\d ВНИМАНИЕ пакет пропущен$"));
            Assert.That(lines[2], Does.Match(@"^\d\d:\d\d:\d\d\.\d\d\d ОШИБКА сбой$"));
        }
    }
}
