using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI
{
    /// <summary>
    /// Курсор в текстовом поле UI Toolkit (CONTROLS §4): пока видимое поле ввода в фокусе, клавиши движения и другие горячие
    /// клавиши молчат. Поле — любое TextInputBaseField (TextField, IntegerField …): у всех общий класс USS.
    /// </summary>
    public static class TextInputFocus
    {
        /// <summary>В фокусе видимое поле ввода (или его часть).</summary>
        public static bool IsEditing(IPanel panel)
        {
            var focused = panel?.focusController?.focusedElement as VisualElement;
            for (var element = focused; element != null; element = element.parent)
            {
                if (element.ClassListContains(TextInputBaseField<string>.ussClassName))
                {
                    return IsDisplayed(element);
                }
            }

            return false;
        }

        /// <summary>Снять фокус: поле экрана входа после входа в мир не должно держать клавиши.</summary>
        public static void Release(IPanel panel) => panel?.focusController?.focusedElement?.Blur();

        // Скрытое поле (панель экрана, которого уже нет) могло остаться в фокусе — оно ввод не держит.
        private static bool IsDisplayed(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (!current.visible || current.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
