namespace TheVerge.Core.Ports
{
    /// <summary>
    /// Сеть глазами ядра (ADR 0013 п. 7). GameClient не знает номеров пакетов (AGENTS.md §2.1): он только просит
    /// обработать следующее, что пришло, и отправить накопленное за тик. Разбор по номеру — в Network.
    /// </summary>
    public interface INetworkSession
    {
        /// <summary>Соединение есть: пакеты приходят и отправка возможна.</summary>
        bool IsConnected { get; }

        /// <summary>Обработать одно событие из очереди (пакет, ошибку, закрытие). false — очередь пуста.</summary>
        bool ProcessNext();

        /// <summary>Отправить всё, что обработчики и команды записали за тик, одним вызовом.</summary>
        void Flush();
    }
}
