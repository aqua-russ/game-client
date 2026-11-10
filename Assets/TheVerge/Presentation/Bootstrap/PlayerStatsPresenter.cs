using System;
using TheVerge.Core;
using UnityEngine;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Презентер характеристик игрока (MVP Presenter).
    /// Связывает адаптер сетевых пакетов IStatsAdapter с моделью состояния PlayerStatsModel
    /// и обновляет отображение отладочного сетевого HUD / консоли.
    /// </summary>
    public sealed class PlayerStatsPresenter : IDisposable
    {
        private readonly IStatsAdapter _statsAdapter;
        private readonly PlayerStatsModel _model;
        private readonly Action<PlayerStatsModel> _onStatsUpdated;

        public PlayerStatsModel Model => _model;

        public PlayerStatsPresenter(
            IStatsAdapter statsAdapter,
            PlayerStatsModel model,
            Action<PlayerStatsModel> onStatsUpdated = null)
        {
            _statsAdapter = statsAdapter ?? throw new ArgumentNullException(nameof(statsAdapter));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _onStatsUpdated = onStatsUpdated;

            _statsAdapter.OnPlayerStatusUpdated += HandlePlayerStatusUpdated;
        }

        private void HandlePlayerStatusUpdated(PlayerStatusDto dto)
        {
            _model.ApplyFullStatus(dto);
            Debug.Log(
                $"[StatsAdapter] 📊 Получены характеристики: Имя={_model.Name}, " +
                $"HP={_model.Hits}/{_model.MaxHits}, Стамина={_model.Stamina}/{_model.MaxStamina}, " +
                $"Мана={_model.Mana}/{_model.MaxMana}, Вес={_model.Weight}/{_model.MaxWeight}, " +
                $"STR={_model.Str}, DEX={_model.Dex}, INT={_model.Int}, Gold={_model.Gold}, Armor={_model.ArmorRating}");

            _onStatsUpdated?.Invoke(_model);
        }

        public void Dispose()
        {
            _statsAdapter.OnPlayerStatusUpdated -= HandlePlayerStatusUpdated;
        }
    }
}
