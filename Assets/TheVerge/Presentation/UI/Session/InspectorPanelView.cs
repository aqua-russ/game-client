using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>Окно клетки отладочной карты поверх мира: текст всех объектов клетки и «Закрыть». Прячется при выходе из мира.</summary>
    public sealed class InspectorPanelView : IDisposable
    {
        private readonly ScrollView _scroll;
        private readonly Label _text;
        private readonly Button _close;

        public InspectorPanelView(VisualElement root)
        {
            Panel = UiElements.Require<VisualElement>(root, "inspectorPanel");
            _scroll = UiElements.Require<ScrollView>(Panel, "inspectorScroll");
            _text = UiElements.Require<Label>(Panel, "inspectorText");
            _close = UiElements.Require<Button>(Panel, "inspectorClose");
            _close.clicked += Hide;
        }

        public VisualElement Panel { get; }

        public void Show(string text)
        {
            _text.text = text;
            _scroll.scrollOffset = Vector2.zero;
            UiElements.Show(Panel, true);
        }

        public void Hide() => UiElements.Show(Panel, false);

        public void Dispose() => _close.clicked -= Hide;
    }
}
