using System;
using System.Collections.Generic;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Показыватель чужих мобов (ADR 0020 пп. 1, 4, 5): ведёт их цилиндры по шагам показа из ядра
    /// (<see cref="OthersMotion.TryGetStep"/>) — плавно из клетки в клетку по доле пройденного шага. Перескок (появление,
    /// телепорт, рывок, отставание — событие <see cref="OthersMotion.Snapped"/>) — отдельный метод <see cref="Snap"/>: только
    /// он ставит цилиндр прямо на клетку. Моба, которого ядро не ведёт (свой персонаж, когда ходьбы нет), — на клетку
    /// сервера. Правил игры здесь нет, клетка → сцена — через <see cref="CellSpace"/>. Вызывается раз в кадр после тика.
    /// </summary>
    public sealed class OthersShower : IDisposable
    {
        private readonly EntityStore _world;
        private readonly OthersMotion _motion;
        private readonly HashSet<uint> _snapPending = new HashSet<uint>();

        public OthersShower(EntityStore world, OthersMotion motion)
        {
            _world = world;
            _motion = motion;
            _motion.Snapped += OnSnapped;
            _world.Removed += OnRemoved;
            _world.Cleared += OnCleared;
        }

        /// <summary>
        /// Поставить цилиндр моба на картинку момента <paramref name="now"/> (часы ядра); <paramref name="isNew"/> — цилиндр
        /// только что взят из пула.
        /// </summary>
        public void Show(Serial serial, Transform marker, bool isNew, long now)
        {
            if (!_motion.TryGetStep(serial, out var step))
            {
                StandOnServerCell(serial, marker);
                return;
            }

            // Перескок — на начало текущего шага: если после перескока в том же тике начался новый шаг, он едет оттуда.
            bool snap = _snapPending.Remove(serial.Value);
            if (snap || isNew)
            {
                Snap(marker, step.From);
            }

            if (step.From != step.To)
            {
                Glide(marker, step, now);
            }
        }

        public void Dispose()
        {
            _motion.Snapped -= OnSnapped;
            _world.Removed -= OnRemoved;
            _world.Cleared -= OnCleared;
        }

        private void OnSnapped(Serial serial, MotionSnap snap) => _snapPending.Add(serial.Value);

        private void OnRemoved(Serial serial) => _snapPending.Remove(serial.Value);

        private void OnCleared() => _snapPending.Clear();

        private void StandOnServerCell(Serial serial, Transform marker)
        {
            var mobile = _world.FindMobile(serial);
            if (mobile != null)
            {
                Snap(marker, mobile.Position);
            }
        }

        private static void Glide(Transform marker, ShownStep step, long now)
        {
            var ground = Vector3.Lerp(GroundOf(step.From), GroundOf(step.To), (float)step.Progress(now));
            marker.position = EntityMarkers.MobileCenter(ground);
        }

        private static void Snap(Transform marker, Point3D cell) => marker.position = EntityMarkers.MobileCenter(GroundOf(cell));

        private static Vector3 GroundOf(Point3D cell) => CellSpace.CellCenter(cell.X, cell.Y, cell.Z);
    }
}
