using System;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI
{
    /// <summary>Поиск элементов разметки по имени и показ/скрытие классом USS (без inline-стилей).</summary>
    public static class UiElements
    {
        /// <summary>Скрытый элемент: класс из USS экрана (display: none).</summary>
        public const string HiddenClass = "hidden";

        /// <summary>Элемент с этим именем обязан быть в UXML: без него экран неполон — понятная ошибка сразу.</summary>
        public static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("В разметке нет элемента " + typeof(T).Name + " с именем «" + name + "».");
            }

            return element;
        }

        public static void Show(VisualElement element, bool visible) => element.EnableInClassList(HiddenClass, !visible);

        /// <summary>Текст подписи; пустая подпись скрыта, чтобы не занимала место.</summary>
        public static void SetText(Label label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }

            Show(label, text.Length > 0);
        }
    }
}
