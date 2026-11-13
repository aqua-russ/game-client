namespace TheVerge.Core.Session
{
    /// <summary>
    /// Часть клиента, которой нужно время: таймеры (ping, ожидание ответа). <see cref="GameClient"/> вызывает
    /// <see cref="Update"/> в каждом тике после разбора пакетов и до отправки — то, что записано, уходит в том же тике.
    /// </summary>
    public interface ITickHandler
    {
        void Update(long now);
    }
}
