using System;
using System.Collections.Generic;
using TheVerge.Core.MapView;
using TheVerge.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Клик левой кнопкой по отладочной карте: луч из камеры → ближайший блок (<see cref="CellPicker"/>; земля, статика,
    /// части домов, мобы и предметы) → окно клетки (<see cref="CellInspector"/>). Клик над панелью интерфейса в мир не идёт.
    /// Вызывается раз в кадр. Ввод — только Input System.
    /// </summary>
    public sealed class CellPicking
    {
        private readonly EntityStore _world;
        private readonly CellInspector _inspector;
        private readonly UnityEngine.Camera _camera;
        private readonly Func<Vector2, bool> _pointerOverUi;
        private readonly Action<string> _report;
        private readonly List<SceneBlock> _markers = new List<SceneBlock>();
        private readonly List<SceneBlock> _all = new List<SceneBlock>();

        /// <param name="inspector">null — файлов данных нет: клик отвечает, что описать клетку нечем.</param>
        /// <param name="pointerOverUi">Позиция курсора на экране → над панелью ли он.</param>
        /// <param name="report">Куда отдать текст окна клетки.</param>
        public CellPicking(EntityStore world, CellInspector inspector, UnityEngine.Camera camera,
            Func<Vector2, bool> pointerOverUi, Action<string> report)
        {
            _world = world;
            _inspector = inspector;
            _camera = camera;
            _pointerOverUi = pointerOverUi;
            _report = report;
        }

        /// <param name="terrain">Блоки карты, которые сейчас нарисованы.</param>
        public void Update(IReadOnlyList<SceneBlock> terrain)
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var position = mouse.position.ReadValue();
            if (!_pointerOverUi(position))
            {
                Pick(position, terrain);
            }
        }

        private void Pick(Vector2 position, IReadOnlyList<SceneBlock> terrain)
        {
            if (_inspector == null)
            {
                _report("Окно клетки недоступно: файлы данных не загружены.");
                return;
            }

            SceneMarkers.Collect(_world, _markers);
            _all.Clear();
            _all.AddRange(terrain);
            _all.AddRange(_markers);
            var ray = CellSpace.ToCellRay(_camera.ScreenPointToRay(position));
            _report(CellPicker.TryPick(_all, ray, out var hit)
                ? _inspector.Describe(hit.X, hit.Y)
                : "Клик мимо карты: под курсором нет клеток.");
        }
    }
}
