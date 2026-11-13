namespace TheVerge.Core.Session.Screens
{
    /// <summary>
    /// Ошибки входа и отказы сервера словами для человека: экраны Unity и бот (PLAN Стадия 1, п. 11, 16). Коды — сервера:
    /// ALRReason, PMMessage, DeleteResultType (Projects/Server/Network/Packets/OutgoingAccountPackets.cs); когда сервер
    /// отказывает в удалении — UOContent/Accounting/AccountHandler.cs, DeleteRequest. Пароля здесь не бывает.
    /// </summary>
    public static class LoginErrorText
    {
        /// <param name="msInWorld">Сколько мс персонаж был в мире до разрыва; -1 — не входил (для подсказки).</param>
        public static string Describe(LoginError error, long msInWorld)
        {
            var detail = string.IsNullOrEmpty(error.Detail) ? string.Empty : " (" + error.Detail + ")";
            switch (error.Kind)
            {
                case LoginErrorKind.LoginDenied:
                    return "Сервер отказал во входе: " + Reject((LoginRejectReason)error.Code) + ".";
                case LoginErrorKind.ConnectFailed when error.Code == (int)DisconnectReason.ConnectTimeout:
                    return "Сервер не ответил на подключение вовремя. Проверьте: запущен ли сервер, верен ли адрес.";
                case LoginErrorKind.ConnectFailed:
                    return "Не удалось подключиться к серверу" + detail + ". Проверьте: запущен ли сервер, верны ли адрес и порт.";
                case LoginErrorKind.CharacterRejected:
                    return "Сервер не пустил персонажа: " + CharacterReject((CharacterRejectReason)error.Code) + ".";
                case LoginErrorKind.Timeout:
                    return "Сервер не ответил вовремя" + detail + ": клиент не ждёт молча, соединение закрыто.";
                case LoginErrorKind.Disconnected when error.Code == (int)DisconnectReason.ServerClosed:
                    var hint = DisconnectHint.For(error, msInWorld);
                    return "Сервер закрыл соединение. " + (hint.Length > 0 ? "Подсказка: " + hint : "Причина — в консоли сервера.");
                case LoginErrorKind.Disconnected:
                    return "Соединение потеряно" + detail + ".";
                case LoginErrorKind.ProtocolError:
                    return "Сервер прислал данные, которые клиент не понимает" + detail + ": соединение разорвано, подробности — в логе.";
                default:
                    return "Вход закончился без известной причины (" + error + ").";
            }
        }

        /// <summary>Почему логин-сервер не пустил (байт отказа сервера).</summary>
        public static string Reject(LoginRejectReason reason)
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

        /// <summary>Почему игровой сервер не пустил персонажа в мир.</summary>
        public static string CharacterReject(CharacterRejectReason reason) =>
            reason == CharacterRejectReason.CharInWorld
                ? "другой персонаж этого аккаунта уже в мире (подождите, пока он выйдет)"
                : "код " + (int)reason;

        /// <summary>Почему сервер не удалил персонажа (0x85).</summary>
        public static string DeleteRejected(CharacterDeleteResult result)
        {
            switch (result)
            {
                case CharacterDeleteResult.PasswordInvalid:
                    return "неверный пароль";
                case CharacterDeleteResult.CharNotExist:
                    return "такого персонажа нет";
                case CharacterDeleteResult.CharBeingPlayed:
                    return "персонаж сейчас в игре";
                case CharacterDeleteResult.CharTooYoung:
                    return "персонаж слишком молод для удаления";
                case CharacterDeleteResult.CharQueued:
                    return "персонаж в очереди сервера, попробуйте позже";
                case CharacterDeleteResult.BadRequest:
                    return "сервер не разрешил удаление (например, персонаж в тюрьме)";
                default:
                    return "код " + (int)result;
            }
        }
    }
}
