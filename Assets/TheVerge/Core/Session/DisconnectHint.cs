namespace TheVerge.Core.Session
{
    /// <summary>Подсказка человеку к ошибке входа: что проверить (PLAN Стадия 1, «Реакция клиента на ошибки»).</summary>
    public static class DisconnectHint
    {
        /// <param name="msInWorld">Сколько мс персонаж был в мире до разрыва; -1 — в мир не входил.</param>
        public static string For(LoginError error, long msInWorld)
        {
            var serverClosed = error.Kind == LoginErrorKind.Disconnected && error.Code == (int)DisconnectReason.ServerClosed;
            if (!serverClosed || msInWorld < 0 || msInWorld >= ServerLimits.ClientVerificationKickDelayMs + KickMarginMs)
            {
                return string.Empty;
            }

            return "сервер закрыл соединение через " + msInWorld / 1000 + " с после входа в мир. Так сервер выгоняет клиент, " +
                   "который счёл неверным: проверьте clientVerification в modernuo.json и версию клиента " +
                   ServerLimits.ClientVersion + " (docs/TARGET.md §1, §5).";
        }

        /// <summary>Запас к задержке сервера: таймер сервера и доставка разрыва.</summary>
        private const int KickMarginMs = 5000;
    }
}
