using System.IO;

namespace TheVerge.Bot
{
    /// <summary>
    /// Где бот берёт и пишет файлы. Корень клиента — первая папка вверх от бота, где есть AGENTS.md и dotnet/ (бот
    /// запускается из dotnet/bin/…). Пароль — remaster/secrets/bot.json: две папки выше корня клиента, вне обоих
    /// git-репозиториев. Логи пакетов — Logs/packets в корне клиента (Logs/ не попадает в git).
    /// </summary>
    public static class BotPaths
    {
        public static string? FindClientRoot(string start)
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(dir.FullName, "dotnet")))
                {
                    return dir.FullName;
                }
            }

            return null;
        }

        public static string DefaultConfig(string clientRoot) =>
            Path.GetFullPath(Path.Combine(clientRoot, "..", "..", "secrets", "bot.json"));

        public static string PacketLogs(string clientRoot) => Path.Combine(clientRoot, "Logs", "packets");
    }
}
