using System.IO;
using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Где бот ищет свои файлы: корень клиента — первая папка вверх, где есть AGENTS.md и dotnet/ (бот запускается из
    /// dotnet/bin/…); пароль — remaster/secrets/bot.json (две папки выше корня клиента, вне обоих git-репозиториев);
    /// логи пакетов — Logs/packets в корне клиента (папка Logs не попадает в git).
    /// </summary>
    public sealed class BotPathsTests
    {
        private string _remaster = string.Empty;

        [SetUp]
        public void SetUp() => _remaster = Path.Combine(Path.GetTempPath(), "theverge-paths-" + Path.GetRandomFileName());

        [TearDown]
        public void TearDown() => Directory.Delete(_remaster, true);

        [Test]
        public void FromBuildFolder_FindsClientRoot_SecretsAndLogs()
        {
            var client = Path.Combine(_remaster, "client", "The Verge");
            var build = Path.Combine(client, "dotnet", "bin", "TheVerge.Bot", "Debug", "net10.0");
            Directory.CreateDirectory(build);
            File.WriteAllText(Path.Combine(client, "AGENTS.md"), "#");

            var root = BotPaths.FindClientRoot(build);

            Assert.That(root, Is.EqualTo(client));
            Assert.That(BotPaths.DefaultConfig(root!), Is.EqualTo(Path.Combine(_remaster, "secrets", "bot.json")));
            Assert.That(BotPaths.PacketLogs(root!), Is.EqualTo(Path.Combine(client, "Logs", "packets")));
        }

        [Test]
        public void NoClientAbove_Null()
        {
            var lonely = Path.Combine(_remaster, "a", "b");
            Directory.CreateDirectory(lonely);

            Assert.That(BotPaths.FindClientRoot(lonely), Is.Null);
        }
    }
}
