using System.Globalization;

namespace TheVerge.Core.Session.Screens
{
    /// <summary>Строка статуса, пока экран ждёт сервер или повтор входа (<see cref="LoginScreens.Status"/>).</summary>
    public static class LoginStatusText
    {
        private static int MaxAttempts => ClientLimits.ReconnectDelaysMs.Length;

        /// <summary>Что сейчас ждём, по состоянию входа; пусто — ничего не ждём (выбирает человек или персонаж в мире).</summary>
        public static string For(LoginState state, string endpoint)
        {
            switch (state)
            {
                case LoginState.Idle:
                case LoginState.ConnectingLogin:
                    return "Подключение к серверу " + endpoint + "…";
                case LoginState.AwaitingShardList:
                    return "Проверка логина и пароля…";
                case LoginState.AwaitingRedirect:
                case LoginState.Redirected:
                case LoginState.ConnectingGame:
                    return "Подключение к шарду…";
                case LoginState.AwaitingCharacterList:
                    return "Загрузка списка персонажей…";
                case LoginState.EnteringWorld:
                case LoginState.WorldLoading:
                    return "Вход в мир…";
                case LoginState.LoggingOut:
                    return "Выход из игры…";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Вход упал, повтор назначен: какая попытка и через сколько.</summary>
        public static string RetryScheduled(LoginErrorKind kind, int attempt, int delayMs)
        {
            var lost = kind == LoginErrorKind.ConnectFailed ? "Не удалось подключиться." : "Связь потеряна.";
            return lost + " Переподключение, попытка " + attempt + " из " + MaxAttempts + " через " + (delayMs + 999) / 1000 + " с…";
        }

        /// <summary>Идёт повторный вход: номер попытки и что ждём сейчас.</summary>
        public static string Retrying(int attempt, string waiting) =>
            "Переподключение, попытка " + attempt + " из " + MaxAttempts +
            (waiting.Length == 0 ? "…" : ": " + char.ToLowerInvariant(waiting[0]) + waiting.Substring(1));

        /// <summary>Строка статуса в мире: имя, задержка до сервера, сколько в мире.</summary>
        public static string InWorld(string name, long rttMs, long msInWorld)
        {
            var rtt = rttMs < 0 ? "—" : rttMs + " мс";
            return name + " · RTT " + rtt + " · в мире " + Duration(msInWorld);
        }

        private static string Duration(long ms)
        {
            var seconds = ms < 0 ? 0 : ms / 1000;
            var hours = seconds / 3600;
            var mmss = (seconds / 60 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                       (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
            return hours > 0 ? hours + ":" + mmss : mmss;
        }
    }
}
