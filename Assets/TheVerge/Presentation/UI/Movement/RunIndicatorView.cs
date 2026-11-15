using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Movement
{
    /// <summary>
    /// Индикатор «Бег / Шаг» внизу экрана (PLAN Стадия 3, п. 1; CONTROLS §4; разметка — UI/Movement/RunIndicator.uxml):
    /// режим, который выбрали клавиши (`~`, Shift), или почему ходьбы нет. Решений здесь нет.
    /// </summary>
    public sealed class RunIndicatorView
    {
        private const string WalkClass = "run-indicator--walk";
        private const string UnavailableClass = "run-indicator--unavailable";
        private readonly Label _label;

        public RunIndicatorView(VisualElement root)
        {
            _label = UiElements.Require<Label>(root, "runIndicator");
            Hide();
        }

        public void ShowMode(bool running)
        {
            _label.text = running ? "Бег" : "Шаг";
            _label.EnableInClassList(WalkClass, !running);
            _label.EnableInClassList(UnavailableClass, false);
            UiElements.Show(_label, true);
        }

        /// <summary>Ходьбы нет: <paramref name="reason"/> — почему, словами.</summary>
        public void ShowUnavailable(string reason)
        {
            _label.text = reason;
            _label.EnableInClassList(WalkClass, false);
            _label.EnableInClassList(UnavailableClass, true);
            UiElements.Show(_label, true);
        }

        public void Hide() => UiElements.Show(_label, false);
    }
}
