#nullable disable // долг прототипа: файл заменяется по docs/TECH-DEBT.md, разметку nullable не делаем
using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Данные подтверждения успешного входа локального игрока в мир (пакет 0x1B).
    /// </summary>
    public readonly struct PlayerLoginConfirmDto
    {
        public Serial PlayerSerial { get; }
        public ushort BodyId { get; }
        public Point3D Position { get; }
        public Direction Direction { get; }

        public PlayerLoginConfirmDto(Serial playerSerial, ushort bodyId, Point3D position, Direction direction)
        {
            PlayerSerial = playerSerial;
            BodyId = bodyId;
            Position = position;
            Direction = direction;
        }
    }

    /// <summary>
    /// Состояние персонажа или NPC в зоне видимости (пакеты 0x78 Mobile Incoming / 0x77 Mobile Moving).
    /// </summary>
    public readonly struct MobileStateDto
    {
        public Serial Serial { get; }
        public ushort BodyId { get; }
        public ushort Hue { get; }
        public Point3D Position { get; }
        public Direction Direction { get; }
        public byte Notoriety { get; }
        public string Name { get; }
        public ushort Hits { get; }
        public ushort MaxHits { get; }
        public bool IsWarMode { get; }

        public MobileStateDto(
            Serial serial,
            ushort bodyId,
            ushort hue,
            Point3D position,
            Direction direction,
            byte notoriety,
            string name = null,
            ushort hits = 100,
            ushort maxHits = 100,
            bool isWarMode = false)
        {
            Serial = serial;
            BodyId = bodyId;
            Hue = hue;
            Position = position;
            Direction = direction;
            Notoriety = notoriety;
            Name = string.IsNullOrEmpty(name) ? (notoriety >= 6 ? "Страж Шахты" : "Добрыня (Кузнец)") : name;
            Hits = hits;
            MaxHits = maxHits > 0 ? maxHits : (ushort)100;
            IsWarMode = isWarMode;
        }
    }

    /// <summary>
    /// Состояние предмета на земле, в сумке или на кукле персонажа.
    /// </summary>
    public readonly struct ItemStateDto
    {
        public Serial Serial { get; }
        public ushort GraphicId { get; }
        public ushort Hue { get; }
        public ushort Amount { get; }
        public Point3D WorldPosition { get; }
        public Serial ContainerSerial { get; }
        public Layer Layer { get; }

        public ItemStateDto(Serial serial, ushort graphicId, ushort hue, ushort amount, Point3D worldPosition, Serial containerSerial, Layer layer)
        {
            Serial = serial;
            GraphicId = graphicId;
            Hue = hue;
            Amount = amount;
            WorldPosition = worldPosition;
            ContainerSerial = containerSerial;
            Layer = layer;
        }
    }

    /// <summary>
    /// Результат шага игрока (пакет 0x22 Move Ack или 0x21 Move Reject).
    /// </summary>
    public readonly struct MoveResultDto
    {
        public byte Sequence { get; }
        public Point3D Position { get; }
        public Direction Direction { get; }

        public MoveResultDto(byte sequence, Point3D position, Direction direction)
        {
            Sequence = sequence;
            Position = position;
            Direction = direction;
        }
    }

    /// <summary>
    /// Характеристики и текущие показатели здоровья, выносливости и маны игрока (пакеты 0x11, 0xA1..0xA3).
    /// </summary>
    public readonly struct PlayerStatusDto
    {
        public Serial Serial { get; }
        public string Name { get; }
        public ushort Hits { get; }
        public ushort MaxHits { get; }
        public ushort Mana { get; }
        public ushort MaxMana { get; }
        public ushort Stamina { get; }
        public ushort MaxStamina { get; }
        public ushort Str { get; }
        public ushort Dex { get; }
        public ushort Int { get; }
        public uint Gold { get; }
        public ushort ArmorRating { get; }
        public ushort Weight { get; }
        public ushort MaxWeight { get; }

        public PlayerStatusDto(
            Serial serial,
            string name,
            ushort hits,
            ushort maxHits,
            ushort mana,
            ushort maxMana,
            ushort stamina,
            ushort maxStamina,
            ushort str,
            ushort dex,
            ushort intel,
            uint gold,
            ushort armorRating,
            ushort weight,
            ushort maxWeight)
        {
            Serial = serial;
            Name = string.IsNullOrEmpty(name) ? "Витязь" : name;
            Hits = hits;
            MaxHits = maxHits > 0 ? maxHits : (ushort)100;
            Mana = mana;
            MaxMana = maxMana > 0 ? maxMana : (ushort)70;
            Stamina = stamina;
            MaxStamina = maxStamina > 0 ? maxStamina : (ushort)80;
            Str = str > 0 ? str : (ushort)90;
            Dex = dex > 0 ? dex : (ushort)80;
            Int = intel > 0 ? intel : (ushort)70;
            Gold = gold;
            ArmorRating = armorRating;
            Weight = weight;
            MaxWeight = maxWeight > 0 ? maxWeight : (ushort)400;
        }

        public static PlayerStatusDto CreateDefault(Serial serial) =>
            new PlayerStatusDto(serial, "Витязь", 100, 100, 70, 70, 80, 80, 90, 80, 70, 250, 18, 35, 400);
    }

    /// <summary>
    /// Информация о нанесённом уроне в бою (пакет 0x0B Damage).
    /// </summary>
    public readonly struct CombatDamageDto
    {
        public Serial TargetSerial { get; }
        public ushort DamageAmount { get; }

        public CombatDamageDto(Serial targetSerial, ushort damageAmount)
        {
            TargetSerial = targetSerial;
            DamageAmount = damageAmount;
        }
    }

    /// <summary>
    /// Текстовое сообщение или реплика над головой объекта/персонажа (пакеты 0x1C / 0xAE).
    /// </summary>
    public readonly struct OverheadMessageDto
    {
        public Serial SourceSerial { get; }
        public string Text { get; }
        public ushort Hue { get; }

        public OverheadMessageDto(Serial sourceSerial, string text, ushort hue = 0x03B2)
        {
            SourceSerial = sourceSerial;
            Text = text ?? string.Empty;
            Hue = hue;
        }
    }

    /// <summary>
    /// Запрос серверного курсора-прицела (пакет 0x6C Target).
    /// </summary>
    public readonly struct TargetCursorRequestDto
    {
        public uint TargetId { get; }
        public bool AllowGround { get; }
        public byte Flags { get; }

        public TargetCursorRequestDto(uint targetId, bool allowGround, byte flags)
        {
            TargetId = targetId;
            AllowGround = allowGround;
            Flags = flags;
        }
    }

    /// <summary>
    /// Параметры визуального 3D-эффекта (пакеты 0x70 / 0xC0 Graphical Effect).
    /// </summary>
    public readonly struct VisualEffectDto
    {
        public byte EffectType { get; }
        public Serial SourceSerial { get; }
        public Serial TargetSerial { get; }
        public ushort GraphicId { get; }
        public Point3D SourcePosition { get; }
        public Point3D TargetPosition { get; }
        public ushort Hue { get; }

        public VisualEffectDto(
            byte effectType,
            Serial sourceSerial,
            Serial targetSerial,
            ushort graphicId,
            Point3D sourcePosition,
            Point3D targetPosition,
            ushort hue = 0)
        {
            EffectType = effectType;
            SourceSerial = sourceSerial;
            TargetSerial = targetSerial;
            GraphicId = graphicId;
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
            Hue = hue;
        }
    }

    /// <summary>
    /// Рецепт кузнечного ремесла на наковальне Минока.
    /// </summary>
    public readonly struct CraftRecipeDto
    {
        public int RecipeId { get; }
        public string Name { get; }
        public ushort ResultGraphicId { get; }
        public ushort RequiredIngots { get; }
        public string Description { get; }

        public CraftRecipeDto(int recipeId, string name, ushort resultGraphicId, ushort requiredIngots, string description)
        {
            RecipeId = recipeId;
            Name = name;
            ResultGraphicId = resultGraphicId;
            RequiredIngots = requiredIngots;
            Description = description;
        }
    }

    /// <summary>
    /// Товар в лавке кузнеца Добрыни (покупка или продажа за золото).
    /// </summary>
    public readonly struct VendorOfferDto
    {
        public int OfferId { get; }
        public string Name { get; }
        public ushort GraphicId { get; }
        public ushort Amount { get; }
        public ushort PriceGold { get; }
        public bool IsPlayerSelling { get; }

        public VendorOfferDto(int offerId, string name, ushort graphicId, ushort amount, ushort priceGold, bool isPlayerSelling)
        {
            OfferId = offerId;
            Name = name;
            GraphicId = graphicId;
            Amount = amount;
            PriceGold = priceGold;
            IsPlayerSelling = isPlayerSelling;
        }
    }
}
