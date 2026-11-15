using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Два показывателя клетки (ADR 0020 п. 1): свой персонаж — <see cref="PlayerShower"/> (если есть ходьба), остальные —
    /// <see cref="OthersShower"/>. Цилиндры раздаёт <see cref="EntityMarkers.ShowMobiles"/>.
    /// </summary>
    public sealed class MobileShowers : IDisposable
    {
        private readonly EntityStore _world;
        private readonly PlayerShower _player;
        private readonly OthersShower _others;

        /// <param name="playerShow">Показ своих шагов; null — ходьбы нет, свой цилиндр стоит на клетке сервера.</param>
        public MobileShowers(EntityStore world, OthersMotion others, PlayerStepShow playerShow)
        {
            _world = world;
            _others = new OthersShower(world, others);
            _player = playerShow == null ? null : new PlayerShower(playerShow);
        }

        public void Show(Serial serial, Transform marker, bool isNew, long now)
        {
            if (_player != null && serial == _world.PlayerSerial)
            {
                _player.Show(marker, isNew, now);
            }
            else
            {
                _others.Show(serial, marker, isNew, now);
            }
        }

        /// <summary>Точка земли под своим персонажем на картинке; false — его показывает клетка сервера (ходьбы нет) или ещё никак.</summary>
        public bool TryGetPlayerGround(out Vector3 ground)
        {
            bool shown = _player != null && _player.IsShown;
            ground = shown ? _player.Ground : Vector3.zero;
            return shown;
        }

        public void Dispose()
        {
            _player?.Dispose();
            _others.Dispose();
        }
    }
}
