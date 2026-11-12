using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Файлы лога пакетов бота: имя — дата и время запуска (по нему же сортировка), бот хранит 20 последних
    /// (docs/TARGET.md §3.1), чужие файлы в папке не трогает.
    /// </summary>
    public sealed class PacketLogFilesTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "theverge-logs-" + Path.GetRandomFileName());

        [TearDown]
        public void TearDown()
        {
            // Не каждый тест создаёт папку: KeepCount_AsAgreed её не трогает.
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Test]
        public void NewFile_NamedByStartTime_FolderCreated()
        {
            var path = PacketLogFiles.NewPath(_dir, new DateTime(2026, 11, 12, 17, 41, 38));

            Assert.That(Path.GetFileName(path), Is.EqualTo("2026-11-12_17-41-38.tsv"));
            Assert.That(Directory.Exists(_dir), Is.True);
        }

        [Test]
        public void SameSecond_SecondFileGetsSuffix()
        {
            var time = new DateTime(2026, 11, 12, 17, 41, 38);
            File.WriteAllText(PacketLogFiles.NewPath(_dir, time), "x");

            Assert.That(Path.GetFileName(PacketLogFiles.NewPath(_dir, time)), Is.EqualTo("2026-11-12_17-41-38_2.tsv"));
        }

        [Test]
        public void Prune_Keeps20Newest_OtherFilesUntouched()
        {
            Directory.CreateDirectory(_dir);
            var start = new DateTime(2026, 11, 1, 10, 0, 0);
            for (var i = 0; i < 25; i++)
            {
                File.WriteAllText(Path.Combine(_dir, start.AddMinutes(i).ToString("yyyy-MM-dd_HH-mm-ss") + ".tsv"), "x");
            }

            File.WriteAllText(Path.Combine(_dir, "мой-важный.tsv"), "x");
            File.WriteAllText(Path.Combine(_dir, "README.txt"), "x");

            PacketLogFiles.Prune(_dir);

            var left = Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.That(left.Count, Is.EqualTo(22));
            Assert.That(left, Does.Contain("мой-важный.tsv").And.Contain("README.txt"));
            Assert.That(left, Does.Not.Contain("2026-11-01_10-04-00.tsv"));
            Assert.That(left, Does.Contain("2026-11-01_10-05-00.tsv").And.Contain("2026-11-01_10-24-00.tsv"));
        }

        [Test]
        public void KeepCount_AsAgreed()
        {
            Assert.That(PacketLogFiles.KeepCount, Is.EqualTo(20));
        }
    }
}
