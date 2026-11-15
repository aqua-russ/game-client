using System;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Показыватель своего персонажа (ADR 0020 пп. 1, 3, 5): ведёт его цилиндр по шагу показа из ядра
    /// (<see cref="PlayerStepShow"/>) — плавно из клетки в клетку по доле пройденного шага. Перескок (вход в мир, откат по
    /// отказу, место от сервера) — отдельный метод <see cref="Snap"/>: только он ставит цилиндр прямо на клетку. Правил игры
    /// здесь нет, клетка → сцена — через <see cref="CellSpace"/>. Вызывается раз в кадр после тика ядра.
    /// </summary>
    public sealed class PlayerShower : IDisposable
    {
        private readonly PlayerStepShow _show;
        private Transform _marker;
        private bool _snapPending = true;

        public PlayerShower(PlayerStepShow show)
        {
            _show = show;
            _show.Snapped += OnSnapped;
        }

        /// <summary>Точка земли под персонажем на картинке (за ней следует камера).</summary>
        public Vector3 Ground { get; private set; }

        /// <summary>Поставить цилиндр на картинку момента <paramref name="now"/> (часы ядра). false — персонажа ещё не ставил сервер.</summary>
        public bool Show(Transform marker, long now)
        {
            if (!_show.IsPlaced)
            {
                return false;
            }

            var step = _show.Current;
            if (marker != _marker)
            {
                _marker = marker;
                _snapPending = true;
            }

            // Перескок — на начало текущего шага: если после отката в том же тике начался новый шаг, он едет от клетки сервера.
            if (_snapPending)
            {
                _snapPending = false;
                Snap(marker, step.From);
            }

            if (step.From != step.To)
            {
                Glide(marker, step, now);
            }

            return true;
        }

        public void Dispose() => _show.Snapped -= OnSnapped;

        private void OnSnapped(MotionSnap snap) => _snapPending = true;

        private void Glide(Transform marker, ShownStep step, long now)
        {
            Ground = Vector3.Lerp(GroundOf(step.From), GroundOf(step.To), (float)step.Progress(now));
            marker.position = EntityMarkers.MobileCenter(Ground);
        }

        private void Snap(Transform marker, Point3D cell)
        {
            Ground = GroundOf(cell);
            marker.position = EntityMarkers.MobileCenter(Ground);
        }

        private static Vector3 GroundOf(Point3D cell) => CellSpace.CellCenter(cell.X, cell.Y, cell.Z);
    }
}
