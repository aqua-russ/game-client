using System;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Movement
{
    /// <summary>
    /// Экран отладки движения (PLAN Стадия 3, п. 9; разметка — UI/Movement/MovementDebug.uxml): строки с числами и кнопка
    /// «Скрыть / Показать», которая сворачивает строки. Числа и тексты даёт <see cref="MovementDebugPresenter"/>; здесь только
    /// показ.
    /// </summary>
    public sealed class MovementDebugView : IDisposable
    {
        // Имена подписей в разметке, по порядку MovementDebugLine.
        private static readonly string[] LineNames =
        {
            "debugCell", "debugMode", "debugPending", "debugAck", "debugResponse", "debugAnswers", "debugJumps",
            "debugOthers", "debugOthersSnaps"
        };

        private readonly VisualElement _panel;
        private readonly VisualElement _body;
        private readonly Button _toggle;
        private readonly Label[] _lines = new Label[LineNames.Length];
        private bool _collapsed;

        public MovementDebugView(VisualElement root)
        {
            _panel = UiElements.Require<VisualElement>(root, "movementDebug");
            _body = UiElements.Require<VisualElement>(_panel, "movementDebugBody");
            _toggle = UiElements.Require<Button>(_panel, "movementDebugToggle");
            for (int i = 0; i < LineNames.Length; i++)
            {
                _lines[i] = UiElements.Require<Label>(_body, LineNames[i]);
            }

            _toggle.clicked += Toggle;
            Show(false);
        }

        public void Show(bool visible) => UiElements.Show(_panel, visible);

        /// <summary>Текст строки; тот же текст повторно не присваивается.</summary>
        public void SetLine(MovementDebugLine line, string text)
        {
            var label = _lines[(int)line];
            if (label.text != text)
            {
                label.text = text;
            }
        }

        public void Dispose() => _toggle.clicked -= Toggle;

        private void Toggle()
        {
            _collapsed = !_collapsed;
            UiElements.Show(_body, !_collapsed);
            _toggle.text = _collapsed ? "Показать" : "Скрыть";
        }
    }
}
