using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Фасад боевых действий, показателей здоровья, горного дела, кузнечного ремесла,
    /// торговли с NPC и применения активных умений/заклинаний (Этапы 5–7).
    /// </summary>
    public interface IGameplayActionsAdapter : IStatsAdapter
    {
        bool IsWarMode { get; }

        /// <summary> Обновление здоровья любого моба/игрока (пакет 0xA1: Serial, Hits, MaxHits) </summary>
        event Action<Serial, ushort, ushort> OnMobileHitsUpdated;

        /// <summary> Всплывающий урон в бою (пакет 0x0B) </summary>
        event Action<CombatDamageDto> OnDamageDealt;

        /// <summary> Замах/атака в бою (пакет 0x2F: Attacker, Defender) </summary>
        event Action<Serial, Serial> OnCombatSwing;

        /// <summary> Проигрывание анимации персонажа/NPC (пакет 0x6E: Mobile, ActionId) </summary>
        event Action<Serial, int> OnMobileAnimation;

        /// <summary> Всплывающий текст над головой объекта/персонажа (пакеты 0x1C / 0xAE) </summary>
        event Action<OverheadMessageDto> OnOverheadMessage;

        /// <summary> Изменение боевой/мирной стойки (пакет 0x72) </summary>
        event Action<bool> OnWarModeChanged;

        /// <summary> Запрос прицела (пакет 0x6C) </summary>
        event Action<TargetCursorRequestDto> OnTargetCursorRequested;

        /// <summary> Визуальный 3D-эффект заклинания, искр или ауры (пакеты 0x70 / 0xC0) </summary>
        event Action<VisualEffectDto> OnVisualEffect;

        /// <summary> Перехват серверного диалога и открытие современного окна UI ("craft", "vendor", "stats") </summary>
        event Action<string> OnGumpWindowOpened;

        void RequestWarMode(bool warMode);
        void RequestAttack(Serial targetMobile);
        void SendTargetResponseGround(uint targetId, Point3D position, ushort staticGraphic = 0);
        void SendTargetResponseEntity(uint targetId, Serial entitySerial, Point3D position, ushort graphicId = 0);

        // Высокоуровневые игровые действия (работают как через стандартные пакеты UO, так и через серверные команды Минока)
        void RequestMineAt(Point3D veinPosition, Serial veinSerial);
        void RequestSmeltOre(Serial forgeSerial);
        void RequestOpenAnvil(Serial anvilSerial);
        void RequestCraftItem(int recipeId);
        void RequestOpenVendor(Serial vendorSerial);
        void RequestVendorTransaction(int offerId);
        void RequestHotbarAbility(int slotIndex, Serial targetMobile);
    }
}
