using UnityEngine;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI
{
    /// <summary>
    /// Единая проверка «курсор над UI» для кликов в мир (AGENTS.md §4): над панелью — клик в мир не идёт. Корневые
    /// контейнеры экранов (<c>screensRoot</c> и всё выше него) занимают весь экран, но в мире они прозрачны: попадание в
    /// них — это пустое место, то есть мир.
    /// </summary>
    public sealed class PointerOverUiGuard
    {
        private readonly VisualElement _backdrop;

        /// <param name="backdrop">Корневой контейнер экранов: он и его родители не считаются интерфейсом.</param>
        public PointerOverUiGuard(VisualElement backdrop)
        {
            _backdrop = backdrop;
        }

        /// <param name="screenPosition">Позиция курсора в пикселях экрана, начало внизу слева (как у Input System).</param>
        public bool IsOver(Vector2 screenPosition)
        {
            var panel = _backdrop.panel;
            if (panel == null)
            {
                return false;
            }

            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped));
            for (var element = _backdrop; element != null; element = element.parent)
            {
                if (element == picked)
                {
                    return false;
                }
            }

            return picked != null;
        }
    }
}
