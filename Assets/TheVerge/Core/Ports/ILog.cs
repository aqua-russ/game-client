namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Лог клиента (ADR 0013 п. 7). Core, Network и Data пишут сюда, а куда это попадёт (Console Unity, консоль бота,
    /// файл), решает Bootstrap. Вызывается только из главного потока.
    /// </summary>
    public interface ILog
    {
        void Info(string message);

        /// <summary>Что-то пошло не так, но работа продолжается (например, пропущен пакет).</summary>
        void Warning(string message);

        /// <summary>Сбой, после которого соединение разорвано.</summary>
        void Error(string message);
    }
}
