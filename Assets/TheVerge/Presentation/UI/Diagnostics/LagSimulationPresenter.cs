using System;
using TheVerge.Core.Diagnostics;

namespace TheVerge.Presentation.UI.Diagnostics
{
    /// <summary>
    /// Режим «симуляция пинга» в Unity (PLAN Стадия 3, п. 9; ADR 0020 п. 9): панель меняет настройки ядра
    /// (<see cref="LagSimulation"/>), а пакеты задерживает сетевой слой. Ядро обрезает значения по пределам — панель сразу
    /// показывает, что вышло. Живёт, пока персонаж в мире; настройки — на весь запуск игры.
    /// </summary>
    public sealed class LagSimulationPresenter : IDisposable
    {
        private readonly LagSimulation _lag;
        private readonly LagSimulationView _view;

        public LagSimulationPresenter(LagSimulation lag, LagSimulationView view)
        {
            _lag = lag;
            _view = view;
            _view.Changed += Apply;
            ShowCurrent();
        }

        public void Dispose() => _view.Changed -= Apply;

        private void Apply()
        {
            _lag.Enabled = _view.Enabled;
            _lag.IncomingDelayMs = _view.IncomingDelayMs;
            _lag.IncomingJitterMs = _view.IncomingJitterMs;
            _lag.OutgoingDelayMs = _view.OutgoingDelayMs;
            _lag.OutgoingJitterMs = _view.OutgoingJitterMs;
            ShowCurrent();
        }

        private void ShowCurrent() => _view.SetValues(_lag.Enabled, _lag.IncomingDelayMs, _lag.IncomingJitterMs, _lag.OutgoingDelayMs,
            _lag.OutgoingJitterMs);
    }
}
