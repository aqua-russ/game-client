using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI
{
    /// <summary>
    /// Список кнопок в контейнере разметки (шарды, слоты персонажей): одна кнопка на строку, нажатие сообщает номер
    /// строки. Подписки на кнопки снимаются при перестройке и в <see cref="Dispose"/>.
    /// </summary>
    public sealed class ButtonList : IDisposable
    {
        private const string ItemClass = "list-item";
        private const string SelectedClass = "list-item--selected";
        private const string EmptyClass = "list-item--empty";
        private readonly VisualElement _container;
        private readonly Action<int> _clicked;
        private readonly List<(Button Button, Action Handler)> _items = new List<(Button, Action)>();

        public ButtonList(VisualElement container, Action<int> clicked)
        {
            _container = container;
            _clicked = clicked;
        }

        public int Count => _items.Count;

        public void Rebuild(int count, Func<int, string> text, Func<int, bool> isEmpty)
        {
            Clear();
            for (var i = 0; i < count; i++)
            {
                var row = i;
                var button = new Button { text = text(i) };
                button.AddToClassList(ItemClass);
                button.EnableInClassList(EmptyClass, isEmpty(i));
                Action handler = () => _clicked(row);
                button.clicked += handler;
                _items.Add((button, handler));
                _container.Add(button);
            }
        }

        public void SetSelected(int selected)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].Button.EnableInClassList(SelectedClass, i == selected);
            }
        }

        public void SetEnabled(bool enabled)
        {
            foreach (var (button, _) in _items)
            {
                button.SetEnabled(enabled);
            }
        }

        public void Dispose() => Clear();

        private void Clear()
        {
            foreach (var (button, handler) in _items)
            {
                button.clicked -= handler;
            }

            _items.Clear();
            _container.Clear();
        }
    }
}
