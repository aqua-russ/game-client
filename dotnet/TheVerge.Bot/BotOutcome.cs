using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>Чем кончился вход бота: сообщение человеку и код выхода (<see cref="BotExitCode"/>). Пароля здесь нет.</summary>
    internal static class BotOutcome
    {
        public static int Report(LoginFlow flow, BotDriver driver, string endpoint, bool interrupted, ILog log)
        {
            if (driver.Problem.Length > 0 && flow.Error.Kind == LoginErrorKind.None)
            {
                log.Error("Бот не может продолжить: " + driver.Problem);
                return BotExitCode.BadArguments;
            }

            var error = flow.Error;
            var detail = string.IsNullOrEmpty(error.Detail) ? string.Empty : " (" + error.Detail + ")";
            switch (error.Kind)
            {
                case LoginErrorKind.None when interrupted:
                    log.Info("Остановлено по Ctrl+C: соединение закрыто.");
                    return BotExitCode.Interrupted;
                case LoginErrorKind.None:
                    log.Info("Готово: соединение закрыто ботом.");
                    return BotExitCode.Success;
                case LoginErrorKind.LoginDenied:
                    log.Error("Сервер отказал во входе: " + RejectText((LoginRejectReason)error.Code) + ".");
                    return BotExitCode.LoginRejected;
                case LoginErrorKind.ConnectFailed when error.Code == (int)DisconnectReason.ConnectTimeout:
                    log.Error("Сервер " + endpoint + " не ответил на подключение вовремя. Запущен ли сервер, верен ли адрес?");
                    return BotExitCode.ConnectFailed;
                case LoginErrorKind.ConnectFailed:
                    log.Error("Не удалось подключиться к " + endpoint + detail + ". Запущен ли сервер, верны ли адрес и порт?");
                    return BotExitCode.ConnectFailed;
                case LoginErrorKind.Disconnected when error.Code == (int)DisconnectReason.ServerClosed:
                    log.Error("Сервер закрыл соединение. Причина — в консоли сервера.");
                    return BotExitCode.ConnectionLost;
                case LoginErrorKind.Disconnected:
                    log.Error("Ошибка сети: соединение потеряно" + detail + ".");
                    return BotExitCode.ConnectionLost;
                case LoginErrorKind.ProtocolError:
                    log.Error("Сервер прислал поток, который клиент не понимает" + detail + ": соединение разорвано, дамп — выше.");
                    return BotExitCode.ProtocolError;
                default:
                    log.Error("Вход закончился без известной причины (" + error + "): ошибка бота.");
                    return BotExitCode.InternalError;
            }
        }

        private static string RejectText(LoginRejectReason reason)
        {
            switch (reason)
            {
                case LoginRejectReason.Invalid:
                    return "нет такого аккаунта";
                case LoginRejectReason.InUse:
                    return "аккаунт уже в игре";
                case LoginRejectReason.Blocked:
                    return "аккаунт заблокирован";
                case LoginRejectReason.BadPassword:
                    return "неверный пароль";
                case LoginRejectReason.Idle:
                    return "клиент слишком долго молчал";
                case LoginRejectReason.BadCommunication:
                    return "сервер не пустил по своей причине (например, с этого адреса нельзя создать ещё один аккаунт: " +
                           "accountHandler.maxAccountsPerIP), подробности — в консоли сервера";
                default:
                    return "код " + (int)reason;
            }
        }
    }
}
