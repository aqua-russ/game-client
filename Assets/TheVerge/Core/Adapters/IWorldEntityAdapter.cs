using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Фасад отслеживания окружающих персонажей, существ и предметов в мире (пакеты 0x78, 0x77, 0xF3, 0x1D).
    /// </summary>
    public interface IWorldEntityAdapter
    {
        /// <summary> Появление персонажа/NPC в зоне видимости (пакет 0x78) </summary>
        event Action<MobileStateDto> OnMobileSpawned;

        /// <summary> Движение персонажа/NPC (пакет 0x77) </summary>
        event Action<MobileStateDto> OnMobileMoved;

        /// <summary> Обновление статуса/направления моба (пакет 0x20) </summary>
        event Action<MobileStateDto> OnMobileUpdated;

        /// <summary> Обновление оверхед-имени моба (пакет 0x98) </summary>
        event Action<Serial, string> OnMobileNameUpdated;

        /// <summary> Появление предмета на земле (пакет 0xF3) </summary>
        event Action<ItemStateDto> OnWorldItemSpawned;

        /// <summary> Удаление объекта из зоны видимости (пакет 0x1D) </summary>
        event Action<Serial> OnEntityDespawned;
    }
}
