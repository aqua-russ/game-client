using System;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Diagnostics
{
    /// <summary>
    /// Панель «Симуляция пинга» на экране отладки движения (PLAN Стадия 3, п. 9; разметка — UI/Diagnostics/LagSimulation.uxml):
    /// переключатель и задержка с разбросом для пакетов от сервера и к серверу. Числа применяются по Enter или уходу из
    /// поля; после этого поле отпускает клавиши (снова можно ходить). Решений нет: что изменилось — событие
    /// <see cref="Changed"/>, значения после проверки ядром — <see cref="SetValues"/>.
    /// </summary>
    public sealed class LagSimulationView : IDisposable
    {
        private readonly Toggle _enabled;
        private readonly IntegerField _incomingDelay;
        private readonly IntegerField _incomingJitter;
        private readonly IntegerField _outgoingDelay;
        private readonly IntegerField _outgoingJitter;
        private readonly EventCallback<ChangeEvent<bool>> _onToggle;
        private readonly EventCallback<ChangeEvent<int>> _onNumber;

        public LagSimulationView(VisualElement root)
        {
            var panel = UiElements.Require<VisualElement>(root, "lagSimulation");
            _enabled = UiElements.Require<Toggle>(panel, "lagEnabled");
            _incomingDelay = UiElements.Require<IntegerField>(panel, "lagInDelay");
            _incomingJitter = UiElements.Require<IntegerField>(panel, "lagInJitter");
            _outgoingDelay = UiElements.Require<IntegerField>(panel, "lagOutDelay");
            _outgoingJitter = UiElements.Require<IntegerField>(panel, "lagOutJitter");
            _onToggle = OnToggle;
            _onNumber = OnNumber;
            _enabled.RegisterValueChangedCallback(_onToggle);
            _incomingDelay.RegisterValueChangedCallback(_onNumber);
            _incomingJitter.RegisterValueChangedCallback(_onNumber);
            _outgoingDelay.RegisterValueChangedCallback(_onNumber);
            _outgoingJitter.RegisterValueChangedCallback(_onNumber);
        }

        /// <summary>Игрок переключил режим или ввёл число.</summary>
        public event Action Changed;

        public bool Enabled => _enabled.value;

        public int IncomingDelayMs => _incomingDelay.value;

        public int IncomingJitterMs => _incomingJitter.value;

        public int OutgoingDelayMs => _outgoingDelay.value;

        public int OutgoingJitterMs => _outgoingJitter.value;

        /// <summary>Показать значения (после проверки ядром), не поднимая <see cref="Changed"/>.</summary>
        public void SetValues(bool enabled, int incomingDelayMs, int incomingJitterMs, int outgoingDelayMs, int outgoingJitterMs)
        {
            _enabled.SetValueWithoutNotify(enabled);
            _incomingDelay.SetValueWithoutNotify(incomingDelayMs);
            _incomingJitter.SetValueWithoutNotify(incomingJitterMs);
            _outgoingDelay.SetValueWithoutNotify(outgoingDelayMs);
            _outgoingJitter.SetValueWithoutNotify(outgoingJitterMs);
        }

        public void Dispose()
        {
            _enabled.UnregisterValueChangedCallback(_onToggle);
            _incomingDelay.UnregisterValueChangedCallback(_onNumber);
            _incomingJitter.UnregisterValueChangedCallback(_onNumber);
            _outgoingDelay.UnregisterValueChangedCallback(_onNumber);
            _outgoingJitter.UnregisterValueChangedCallback(_onNumber);
        }

        private void OnToggle(ChangeEvent<bool> change) => Changed?.Invoke();

        // Поле отложенное (is-delayed): значение приходит по Enter или уходу из поля — после него фокус снимается, чтобы
        // клавиши движения снова работали без щелчка в мир.
        private void OnNumber(ChangeEvent<int> change)
        {
            Changed?.Invoke();
            (change.target as Focusable)?.Blur();
        }
    }
}
