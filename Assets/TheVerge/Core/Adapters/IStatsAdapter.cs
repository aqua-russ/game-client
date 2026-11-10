using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Фасад характеристик и статуса персонажа (пакеты 0x11, 0xA1, 0xA2, 0xA3, 0x2D).
    /// </summary>
    public interface IStatsAdapter
    {
        /// <summary> Текущие кэшированные характеристики локального игрока </summary>
        PlayerStatusDto CurrentPlayerStatus { get; }

        /// <summary> Событие обновления характеристик локального игрока (пакеты 0x11, 0xA1..0xA3, 0x2D) </summary>
        event Action<PlayerStatusDto> OnPlayerStatusUpdated;

        /// <summary> Запрос характеристик сущности с сервера (пакет 0x34 Mobile Query type 4) </summary>
        void RequestStats(Serial mobileSerial);
    }
}
