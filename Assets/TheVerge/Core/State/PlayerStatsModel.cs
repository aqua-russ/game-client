using System;

namespace TheVerge.Core
{
    /// <summary>
    /// Модель состояния и характеристик игрока в памяти клиента (MVP Model).
    /// Хранит актуальные показатели локального персонажа и уведомляет об изменениях.
    /// Чистый C#-класс без UnityEngine.
    /// </summary>
    public sealed class PlayerStatsModel
    {
        public Serial Serial { get; private set; }
        public string Name { get; private set; } = string.Empty;

        public ushort Hits { get; private set; }
        public ushort MaxHits { get; private set; }
        public ushort Mana { get; private set; }
        public ushort MaxMana { get; private set; }
        public ushort Stamina { get; private set; }
        public ushort MaxStamina { get; private set; }

        public ushort Str { get; private set; }
        public ushort Dex { get; private set; }
        public ushort Int { get; private set; }

        public ushort Weight { get; private set; }
        public ushort MaxWeight { get; private set; }
        public uint Gold { get; private set; }
        public ushort ArmorRating { get; private set; }

        public float HitsPercent => MaxHits > 0 ? (float)Hits / MaxHits : 0f;
        public float ManaPercent => MaxMana > 0 ? (float)Mana / MaxMana : 0f;
        public float StaminaPercent => MaxStamina > 0 ? (float)Stamina / MaxStamina : 0f;
        public float WeightPercent => MaxWeight > 0 ? (float)Weight / MaxWeight : 0f;
        public bool IsOverloaded => MaxWeight > 0 && Weight > MaxWeight;
        public bool IsFatigued => Stamina == 0;
        public bool IsCriticallyOverloaded => MaxWeight > 0 && (Weight >= (ushort)(MaxWeight * 1.5f) || (Weight > MaxWeight && Stamina == 0));
        public float OverweightRatio => MaxWeight > 0 ? (float)Weight / MaxWeight : 1f;

        public event Action<PlayerStatsModel> OnStatsChanged;

        public void ApplyFullStatus(PlayerStatusDto dto)
        {
            Serial = dto.Serial;
            Name = dto.Name;
            Hits = dto.Hits;
            MaxHits = dto.MaxHits;
            Mana = dto.Mana;
            MaxMana = dto.MaxMana;
            Stamina = dto.Stamina;
            MaxStamina = dto.MaxStamina;
            Str = dto.Str;
            Dex = dto.Dex;
            Int = dto.Int;
            Weight = dto.Weight;
            MaxWeight = dto.MaxWeight;
            Gold = dto.Gold;
            ArmorRating = dto.ArmorRating;

            OnStatsChanged?.Invoke(this);
        }

        public void ApplyHits(ushort hits, ushort maxHits)
        {
            Hits = hits;
            if (maxHits > 0) MaxHits = maxHits;
            OnStatsChanged?.Invoke(this);
        }

        public void ApplyMana(ushort mana, ushort maxMana)
        {
            Mana = mana;
            if (maxMana > 0) MaxMana = maxMana;
            OnStatsChanged?.Invoke(this);
        }

        public void ApplyStamina(ushort stamina, ushort maxStamina)
        {
            Stamina = stamina;
            if (maxStamina > 0) MaxStamina = maxStamina;
            OnStatsChanged?.Invoke(this);
        }

        public PlayerStatusDto ToDto() =>
            new PlayerStatusDto(
                Serial, Name, Hits, MaxHits, Mana, MaxMana, Stamina, MaxStamina,
                Str, Dex, Int, Gold, ArmorRating, Weight, MaxWeight);
    }
}
