using System;
using System.Threading;

namespace TheVerge.Network.Transport
{
    /// <summary>
    /// Одно TCP-подключение к серверу (PLAN Стадия 1, п. 4). Всё, что приходит, — события в очереди
    /// (<see cref="TryReceive"/>): подключение, целые пакеты, ошибка нарезки, закрытие. Главный поток ничего не ждёт:
    /// подключение, приём и отправка идут без блокировки. Интерфейс нужен, чтобы в тестах подставлять свой транспорт.
    /// </summary>
    public interface IConnection : IDisposable
    {
        ConnectionState State { get; }

        /// <summary>Начать подключение. Итог — событие Connected или Closed. Повторный вызов — ошибка.</summary>
        void Connect(string host, int port, CancellationToken cancellation);

        /// <summary>Включить распаковку Huffman. Вызывается до отправки 0x91 (игровой сокет).</summary>
        void EnableCompression();

        /// <summary>Отправить байты без блокировки. false — соединения нет (ещё не подключено или уже закрыто).</summary>
        bool Send(ReadOnlySpan<byte> data);

        /// <summary>Следующее событие. Буфер пакета вернуть через InboundEvent.Release после обработки.</summary>
        bool TryReceive(out InboundEvent item);
    }
}
