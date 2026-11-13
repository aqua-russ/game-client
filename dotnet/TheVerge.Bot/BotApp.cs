using System;
using System.Buffers;
using System.IO;
using System.Threading;
using TheVerge.Core.Session;
using TheVerge.Network.Transport;

namespace TheVerge.Bot
{
    /// <summary>
    /// Бот целиком (PLAN Стадия 1, п. 15): параметры (<see cref="BotArguments"/>), настройки и пароль
    /// (<see cref="BotConfigFile"/> или скрытый ввод), лог пакетов в файл (<see cref="BotPacketLog"/>), запуск
    /// <see cref="BotRunner"/>, итог и код выхода. Пароль никуда не печатается.
    /// </summary>
    public sealed class BotApp
    {
        private readonly TextWriter _output;
        private readonly string? _clientRoot;
        private readonly Func<string?>? _passwordPrompt;

        /// <param name="clientRoot">Корень клиента (AGENTS.md); null — не найден: нужен --config, лог пакетов не пишется.</param>
        /// <param name="passwordPrompt">Скрытый ввод пароля; null — спросить некого (запуск из скрипта).</param>
        public BotApp(TextWriter output, string? clientRoot, Func<string?>? passwordPrompt)
        {
            _output = output;
            _clientRoot = clientRoot;
            _passwordPrompt = passwordPrompt;
        }

        public int Run(string[] args, CancellationToken interrupt)
        {
            try
            {
                return RunChecked(BotArguments.Parse(args), interrupt);
            }
            catch (Exception e)
            {
                // Последний рубеж: ошибка в самом боте — с подробностями, чтобы её можно было найти.
                return Exit(BotExitCode.InternalError, "ОШИБКА бота: " + e);
            }
        }

        private int RunChecked(ParsedArguments parsed, CancellationToken interrupt)
        {
            if (parsed.Error.Length > 0)
            {
                return Exit(BotExitCode.BadArguments, "ОШИБКА: " + parsed.Error);
            }

            switch (parsed.Mode)
            {
                case BotMode.Help:
                    _output.Write(BotUsage.Text);
                    return BotExitCode.Success;
                case BotMode.SelfCheck:
                    return BotSelfCheck.Run(_output);
            }

            var settings = LoadSettings(parsed, out var problem);
            return settings == null
                ? Exit(BotExitCode.BadArguments, "ОШИБКА: " + problem)
                : Connect(settings, parsed, interrupt);
        }

        private BotSettings? LoadSettings(ParsedArguments parsed, out string problem)
        {
            var path = parsed.ConfigPath ?? (_clientRoot == null ? null : BotPaths.DefaultConfig(_clientRoot));
            if (path == null)
            {
                problem = "не найдена папка клиента (с AGENTS.md), а значит и файл настроек. Укажите его: --config <путь>.";
                return null;
            }

            var loaded = BotConfigFile.Load(path, parsed.Overrides);
            if (loaded.Warning.Length > 0)
            {
                _output.WriteLine("ВНИМАНИЕ: " + loaded.Warning);
            }

            problem = loaded.Error.Length > 0 ? loaded.Error : CompleteCredentials(loaded.Settings, path);
            return problem.Length == 0 ? loaded.Settings : null;
        }

        private string CompleteCredentials(BotSettings settings, string path)
        {
            if (settings.Login.Length == 0)
            {
                return "не задан логин: сохраните его командой tools/bot/setup.sh (файл " + path + ") или укажите --login.";
            }

            if (settings.Password.Length == 0 && _passwordPrompt != null)
            {
                settings.Password = _passwordPrompt() ?? string.Empty;
            }

            if (settings.Password.Length == 0)
            {
                return "нет пароля: сохраните его один раз командой tools/bot/setup.sh (файл " + path + ").";
            }

            return FitsServer(settings.Login) && FitsServer(settings.Password)
                ? string.Empty
                : "логин и пароль — не длиннее " + ServerLimits.CredentialMaxChars + " символов, только латиница " +
                  "(сервер читает их в Latin-1). Исправьте командой tools/bot/setup.sh.";
        }

        private int Connect(BotSettings settings, ParsedArguments parsed, CancellationToken interrupt)
        {
            var log = new ConsoleLog(_output);
            var clock = new StopwatchClock();
            using (var packetLog = parsed.PacketLog && _clientRoot != null ? BotPacketLog.Open(_clientRoot, settings, clock, log) : null)
            {
                if (packetLog != null)
                {
                    log.Info("Лог пакетов: " + packetLog.Path);
                }

                var credentials = new LoginCredentials(settings.Login, settings.Password);
                var login = new BotLogin(settings.Host, settings.Port, credentials, settings.Character, parsed.CreateCharacter)
                {
                    IdleSeconds = parsed.IdleSeconds,
                };
                var code = new BotRunner(NewConnection, clock, log, packetLog?.Writer).Run(login, interrupt);
                return Exit(code, string.Empty);
            }
        }

        private static bool FitsServer(string value)
        {
            if (value.Length > ServerLimits.CredentialMaxChars)
            {
                return false;
            }

            foreach (var c in value)
            {
                if (c > ServerLimits.MaxLatin1Char)
                {
                    return false;
                }
            }

            return true;
        }

        private static IConnection NewConnection() =>
            new TcpConnection(ArrayPool<byte>.Shared, ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes);

        private int Exit(int code, string message)
        {
            if (message.Length > 0)
            {
                _output.WriteLine(message);
            }

            _output.WriteLine("Код выхода " + code + ".");
            return code;
        }
    }
}
