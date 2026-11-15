using System;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.MapView;
using TheVerge.Core.Ports;
using TheVerge.Presentation.Input;
using TheVerge.Presentation.UI;
using TheVerge.Presentation.UI.Diagnostics;
using TheVerge.Presentation.UI.Movement;
using TheVerge.Presentation.World;
using UnityEngine;

namespace TheVerge.Bootstrap
{
    /// <summary>
    /// Хозяин отладочной карты и ходьбы (PLAN Стадия 2, п. 13; Стадия 3, п. 9): на время «в мире» создаёт карту
    /// (<see cref="DebugMapPresenter"/>), клавиши движения (<see cref="MovementInputPresenter"/>), экран отладки движения
    /// (<see cref="MovementDebugPresenter"/>) и панель «Симуляция пинга» (<see cref="LagSimulationPresenter"/>), при выходе из
    /// мира или новом входе закрывает их. Файлы данных — <see cref="WorldDataSource"/>; без них карты нет, а ходьбы нет — и об этом
    /// говорит индикатор «Бег / Шаг».
    /// </summary>
    public sealed class DebugMapHost : IDisposable
    {
        private const string NoWalkingText = "Ходьба недоступна: не найдены файлы данных (папка и причина — в Console)";

        private readonly ILog _log;
        private readonly Camera _camera;
        private readonly WorldDataSource _data;
        private readonly WorldUiHooks _ui;
        private readonly LagSimulation _lag;
        private UnityWorld _world;
        private DebugMapPresenter _presenter;
        private MovementInputPresenter _input;
        private MovementDebugPresenter _debug;
        private LagSimulationPresenter _lagPanel;

        /// <param name="lag">Настройки симуляции пинга — их меняет панель на экране отладки.</param>
        public DebugMapHost(ILog log, Camera camera, WorldDataSource data, WorldUiHooks ui, LagSimulation lag)
        {
            _log = log;
            _camera = camera;
            _data = data;
            _ui = ui;
            _lag = lag;
        }

        /// <summary>Что показать на панели «в мире»; пусто — ничего.</summary>
        public string Status => _presenter != null ? _presenter.Status : string.Empty;

        /// <summary>Раз в кадр до тика ядра: клавиши движения → намерение, чтобы нажатие и шаг пришлись на один кадр.</summary>
        public void ReadInput() => _input?.Update(_presenter.CameraLayout);

        /// <summary>Раз в кадр после тика ядра: <paramref name="world"/> — мир текущего входа (null — клиента нет).</summary>
        /// <param name="now">Часы ядра (IClock).</param>
        public void Update(bool inWorld, UnityWorld world, long now)
        {
            if (!inWorld || world == null || world != _world)
            {
                Close();
            }

            if (!inWorld || world == null)
            {
                return;
            }

            if (_presenter == null)
            {
                Open(world);
            }

            _presenter.Update(now);
            _debug.Update(now);
        }

        public void Dispose() => Close();

        private void Open(UnityWorld world)
        {
            _world = world;
            var data = _data.Load();
            var entities = world.Entities;
            var builder = data == null ? null : new SceneBuilder(data.Map, data.Tiles, data.Multis, entities);
            var inspector = data == null ? null : new CellInspector(data.Map, data.Tiles, data.Multis, entities);
            var movement = world.Movement;
            _presenter = new DebugMapPresenter(new DebugMapParts(entities, builder, inspector, world.Others, movement?.Show), _camera, _ui);
            var player = movement == null
                ? default
                : new PlayerMovementParts(movement.Motion, movement.Replies, movement.Pacer, movement.Latency, movement.Show);
            var panels = _ui.Panels;
            _debug = new MovementDebugPresenter(player, world.Others, panels.MovementDebug);
            _lagPanel = new LagSimulationPresenter(_lag, panels.LagSimulation);
            if (movement != null)
            {
                _input = new MovementInputPresenter(movement.Intent, panels.RunIndicator, _ui.TextInputFocused);
            }
            else
            {
                panels.RunIndicator.ShowUnavailable(NoWalkingText);
            }

            if (data == null)
            {
                _log.Warning("Отладочная карта без земли и без ходьбы: " + _data.Problem);
            }
        }

        private void Close()
        {
            if (_presenter == null)
            {
                return;
            }

            _input?.Dispose();
            _input = null;
            _debug.Dispose();
            _debug = null;
            _lagPanel.Dispose();
            _lagPanel = null;
            _presenter.Dispose();
            _presenter = null;
            _world = null;
            _ui.Panels.RunIndicator.Hide();
        }
    }
}
