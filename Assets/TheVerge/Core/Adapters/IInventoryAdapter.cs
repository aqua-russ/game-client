using System;
using System.Collections.Generic;

namespace TheVerge.Core
{
    /// <summary>
    /// Фасад работы с инвентарём, предметами, контейнерами и экипировкой
    /// (пакеты 0x06, 0x07, 0x08, 0x13, 0x1D, 0x24, 0x25, 0x27, 0x2E, 0x3C).
    /// </summary>
    public interface IInventoryAdapter
    {
        /// <summary> Serial рюкзака локального игрока </summary>
        Serial PlayerBackpackSerial { get; }

        /// <summary> Двойной клик: открыть сумку/сундук/дверь, применить предмет (0x06) </summary>
        void UseItem(Serial itemSerial);

        /// <summary> Поднять предмет с земли, из сумки или снять с куклы (0x07 Lift Request) </summary>
        void LiftItem(Serial itemSerial, ushort amount = 1);

        /// <summary> Бросить удерживаемый предмет на землю в указанную точку (0x08 Drop Request) </summary>
        void DropToWorld(Serial itemSerial, Point3D targetPosition);

        /// <summary>
        /// Положить удерживаемый предмет в контейнер (0x08 Drop Request).
        /// Согласно правилу из AGENTS.md, всегда передаются фиктивные координаты X=0, Y=0.
        /// </summary>
        void DropToContainer(Serial itemSerial, Serial targetContainerSerial);

        /// <summary> Надеть удерживаемый предмет на куклу персонажа (0x13 Equip Request) </summary>
        void EquipItem(Serial itemSerial, Layer targetLayer);

        /// <summary> Закрыть открытый контейнер (запрос игрока или закрытие клиентом) </summary>
        void CloseContainer(Serial containerSerial);

        /// <summary> Сервер прислал команду открыть окно контейнера (пакет 0x24) </summary>
        event Action<Serial, ushort> OnContainerOpened; // containerSerial, gumpId

        /// <summary> Контейнер закрыт сервером или игрок отошёл слишком далеко </summary>
        event Action<Serial> OnContainerClosed; // containerSerial

        /// <summary> Сервер прислал содержимое контейнера (пакет 0x3C) </summary>
        event Action<Serial, IReadOnlyList<ItemStateDto>> OnContainerContentReceived;

        /// <summary> Добавлен один предмет в контейнер (пакет 0x25) </summary>
        event Action<ItemStateDto> OnItemAddedToContainer;

        /// <summary> Предмет извлечён из контейнера </summary>
        event Action<Serial, Serial> OnItemRemovedFromContainer; // containerSerial, itemSerial

        /// <summary> Предмет надет на персонажа (пакет 0x2E) </summary>
        event Action<Serial, ItemStateDto> OnItemEquipped; // mobileSerial, item

        /// <summary> Предмет снят со слоя персонажа </summary>
        event Action<Serial, Layer> OnItemUnequipped; // mobileSerial, layer

        /// <summary> Отказ сервера поднять предмет или слишком далеко (пакет 0x27) </summary>
        event Action<byte> OnLiftRejected;
    }
}
