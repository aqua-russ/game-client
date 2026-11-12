using System;
using System.IO;
using System.Text.Json;

namespace TheVerge.Bot
{
    /// <summary>
    /// Файл настроек бота (JSON: host, port, login, password, character). Лежит в remaster/secrets/bot.json — вне обоих
    /// git-репозиториев, создаётся tools/bot/setup.sh с правами только для владельца. Файла нет — не ошибка: всё берётся из
    /// командной строки и значений по умолчанию, а пароль бот спросит. Командная строка главнее файла. Содержимое файла
    /// в сообщения об ошибках не попадает: в нём пароль.
    /// </summary>
    public static class BotConfigFile
    {
        private const UnixFileMode OthersAccess =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

        public static LoadedConfig Load(string path, BotOverrides overrides)
        {
            var loaded = new LoadedConfig();
            if (File.Exists(path))
            {
                ReadFile(path, loaded);
            }

            Apply(overrides, loaded.Settings);
            return loaded;
        }

        private static void ReadFile(string path, LoadedConfig loaded)
        {
            try
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    loaded.Error = ReadFields(document.RootElement, loaded.Settings, path);
                }
            }
            catch (JsonException e)
            {
                loaded.Error = "файл настроек " + path + " испорчен (не JSON, строка " + (e.LineNumber + 1) +
                               "). Исправьте его или создайте заново: tools/bot/setup.sh.";
            }

            if (loaded.Error.Length == 0 && !OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & OthersAccess) != 0)
            {
                loaded.Warning = "файл " + path + " с паролем могут читать другие пользователи компьютера. Закройте доступ: chmod 600 \"" + path + "\".";
            }
        }

        private static string ReadFields(JsonElement root, BotSettings settings, string path)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return "файл настроек " + path + ": ожидается объект { \"login\": …, \"password\": … }.";
            }

            foreach (var field in root.EnumerateObject())
            {
                var problem = ReadField(field, settings);
                if (problem.Length > 0)
                {
                    return "файл настроек " + path + ": поле «" + field.Name + "» " + problem;
                }
            }

            return string.Empty;
        }

        private static string ReadField(JsonProperty field, BotSettings settings)
        {
            if (field.Name == "port")
            {
                if (field.Value.ValueKind != JsonValueKind.Number || !field.Value.TryGetInt32(out var port) || port < 1 || port > ushort.MaxValue)
                {
                    return "должно быть числом от 1 до 65535.";
                }

                settings.Port = port;
                return string.Empty;
            }

            if (field.Value.ValueKind != JsonValueKind.String)
            {
                return "должно быть строкой.";
            }

            var text = field.Value.GetString() ?? string.Empty;
            switch (field.Name)
            {
                case "host":
                    settings.Host = text;
                    break;
                case "login":
                    settings.Login = text;
                    break;
                case "password":
                    settings.Password = text;
                    break;
                case "character":
                    settings.Character = text;
                    break;
            }

            return string.Empty;
        }

        private static void Apply(BotOverrides overrides, BotSettings settings)
        {
            settings.Host = overrides.Host ?? settings.Host;
            settings.Port = overrides.Port ?? settings.Port;
            settings.Login = overrides.Login ?? settings.Login;
            settings.Character = overrides.Character ?? settings.Character;
        }
    }
}
