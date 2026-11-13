using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;

namespace TheVerge.Bot
{
    /// <summary>Чем кончился вход бота: сообщение человеку и код выхода (<see cref="BotExitCode"/>). Пароля здесь нет.</summary>
    internal static class BotOutcome
    {
        /// <param name="msInWorld">Сколько мс персонаж был в мире до конца; -1 — в мир не входил (для подсказки при разрыве).</param>
        public static int Report(LoginFlow flow, BotDriver driver, string endpoint, bool interrupted, long msInWorld, ILog log)
        {
            if (driver.Problem.Length > 0 && flow.Error.Kind == LoginErrorKind.None)
            {
                log.Error("Бот не может продолжить: " + driver.Problem);
                return BotExitCode.BadArguments;
            }

            if (flow.Error.Kind == LoginErrorKind.None)
            {
                log.Info(interrupted ? "Остановлено по Ctrl+C: соединение закрыто." :
                    driver.LoggedOut ? "Вышел из игры: соединение закрыто ботом." : "Готово: соединение закрыто ботом.");
                return interrupted ? BotExitCode.Interrupted : BotExitCode.Success;
            }

            return ReportError(flow.Error, endpoint, msInWorld, log);
        }

        private static int ReportError(LoginError error, string endpoint, long msInWorld, ILog log)
        {
            var detail = string.IsNullOrEmpty(error.Detail) ? string.Empty : " (" + error.Detail + ")";
            switch (error.Kind)
            {
                case LoginErrorKind.LoginDenied:
                    log.Error("Сервер отказал во входе: " + LoginErrorText.Reject((LoginRejectReason)error.Code) + ".");
                    return BotExitCode.LoginRejected;
                case LoginErrorKind.ConnectFailed when error.Code == (int)DisconnectReason.ConnectTimeout:
                    log.Error("Сервер " + endpoint + " не ответил на подключение вовремя. Запущен ли сервер, верен ли адрес?");
                    return BotExitCode.ConnectFailed;
                case LoginErrorKind.ConnectFailed:
                    log.Error("Не удалось подключиться к " + endpoint + detail + ". Запущен ли сервер, верны ли адрес и порт?");
                    return BotExitCode.ConnectFailed;
                case LoginErrorKind.CharacterRejected:
                    log.Error("Сервер не пустил персонажа: " + LoginErrorText.CharacterReject((CharacterRejectReason)error.Code) + ".");
                    return BotExitCode.LoginRejected;
                case LoginErrorKind.Timeout:
                    log.Error("Сервер не ответил вовремя" + detail + ": бот не ждёт молча, соединение закрыто.");
                    return BotExitCode.ConnectionLost;
                case LoginErrorKind.Disconnected when error.Code == (int)DisconnectReason.ServerClosed:
                    var hint = DisconnectHint.For(error, msInWorld);
                    log.Error("Сервер закрыл соединение. " + (hint.Length > 0 ? "Подсказка: " + hint : "Причина — в консоли сервера."));
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
    }
}
