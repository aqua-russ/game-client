using System;
using TheVerge.Presentation.UI.Movement;
using UnityEngine;

namespace TheVerge.Presentation.UI
{
    /// <summary>
    /// Что мир (отладочная карта, клавиши движения) берёт у интерфейса поверх сцены: курсор над панелью (клик в мир тогда
    /// не идёт), окно клетки, курсор в текстовом поле (клавиши молчат) и индикатор «Бег / Шаг».
    /// </summary>
    public sealed class WorldUiHooks
    {
        /// <param name="pointerOverUi">Позиция курсора на экране → над панелью ли он.</param>
        /// <param name="showReport">Показать окно клетки (клик по клетке).</param>
        /// <param name="textInputFocused">Курсор в видимом поле ввода.</param>
        public WorldUiHooks(Func<Vector2, bool> pointerOverUi, Action<string> showReport, Func<bool> textInputFocused,
            RunIndicatorView runIndicator)
        {
            PointerOverUi = pointerOverUi;
            ShowReport = showReport;
            TextInputFocused = textInputFocused;
            RunIndicator = runIndicator;
        }

        public Func<Vector2, bool> PointerOverUi { get; }

        public Action<string> ShowReport { get; }

        public Func<bool> TextInputFocused { get; }

        public RunIndicatorView RunIndicator { get; }
    }
}
