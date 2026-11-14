using System;
using TheVerge.Core.Session.Screens;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>Строка «в мире» поверх сцены: имя, RTT, время в мире и «Выйти».</summary>
    public sealed class WorldPanelView : IDisposable
    {
        private readonly LoginScreens _screens;
        private readonly Label _status;
        private readonly Label _mapStatus;
        private readonly Button _logout;

        public WorldPanelView(VisualElement root, LoginScreens screens)
        {
            _screens = screens;
            Panel = UiElements.Require<VisualElement>(root, "worldPanel");
            _status = UiElements.Require<Label>(Panel, "worldStatus");
            _mapStatus = UiElements.Require<Label>(Panel, "mapStatus");
            _logout = UiElements.Require<Button>(Panel, "logoutButton");
            _logout.clicked += _screens.Logout;
        }

        public VisualElement Panel { get; }

        public void Refresh() => _status.text = _screens.WorldStatus;

        /// <summary>Текст отладочной карты; тот же текст повторно не присваивается.</summary>
        public void SetMapStatus(string text)
        {
            if (_mapStatus.text != text)
            {
                _mapStatus.text = text;
            }
        }

        public void Dispose() => _logout.clicked -= _screens.Logout;
    }
}
