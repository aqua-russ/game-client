using System;
using TheVerge.Core.Session.Screens;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>
    /// Экраны входа на UI Toolkit (разметка — UI/Session/LoginScreens.uxml): показывает панель текущего экрана
    /// <see cref="LoginScreens"/> и обновляет её, только когда модель что-то поменяла (<see cref="LoginScreens.Revision"/>),
    /// поэтому кадр без изменений ничего не делает. Решений здесь нет — только показ и передача нажатий.
    /// </summary>
    public sealed class LoginScreensView : IDisposable
    {
        private const string InWorldClass = "screens-root--in-world";
        private readonly LoginScreens _screens;
        private readonly VisualElement _root;
        private readonly LoginPanelView _login;
        private readonly ShardsPanelView _shards;
        private readonly CharactersPanelView _characters;
        private readonly CreatePanelView _create;
        private readonly WorldPanelView _world;
        private readonly InspectorPanelView _inspector;
        private readonly PointerOverUiGuard _guard;
        private readonly VisualElement _waiting;
        private readonly Label _waitingStatus;
        private readonly Button _waitingCancel;
        private int _seenRevision = -1;
        private LoginScreen _shownScreen = LoginScreen.Login;

        public LoginScreensView(VisualElement root, LoginScreens screens)
        {
            _screens = screens;
            _root = UiElements.Require<VisualElement>(root, "screensRoot");
            _login = new LoginPanelView(_root, screens);
            _shards = new ShardsPanelView(_root, screens);
            _characters = new CharactersPanelView(_root, screens);
            _create = new CreatePanelView(_root, screens);
            _world = new WorldPanelView(_root, screens);
            _inspector = new InspectorPanelView(_root);
            _guard = new PointerOverUiGuard(_root);
            _waiting = UiElements.Require<VisualElement>(_root, "waitingPanel");
            _waitingStatus = UiElements.Require<Label>(_waiting, "waitingStatus");
            _waitingCancel = UiElements.Require<Button>(_waiting, "waitingCancel");
            _waitingCancel.clicked += _screens.Cancel;
        }

        /// <summary>Раз в кадр после тика модели.</summary>
        public void Refresh()
        {
            var revision = _screens.Revision;
            if (revision == _seenRevision)
            {
                return;
            }

            _seenRevision = revision;
            var screen = _screens.Screen;
            var enteringWorld = screen == LoginScreen.InWorld && _shownScreen != LoginScreen.InWorld;
            if (screen == LoginScreen.CreateCharacter && _shownScreen != LoginScreen.CreateCharacter)
            {
                _create.Load();
            }

            _shownScreen = screen;
            ShowOnly(screen);
            RefreshPanel(screen);
            if (enteringWorld)
            {
                // Поле пароля могло остаться в фокусе: в мире клавиши движения не должны ему достаться.
                TextInputFocus.Release(_root.panel);
            }
        }

        /// <summary>Строка отладочной карты на панели «в мире»: что нарисовано или почему карты нет.</summary>
        public void SetMapStatus(string text) => _world.SetMapStatus(text);

        /// <summary>Окно клетки отладочной карты поверх мира (клик по клетке).</summary>
        public void ShowCellReport(string text) => _inspector.Show(text);

        /// <summary>Курсор (пиксели экрана, начало внизу слева) над панелью интерфейса — клик в мир не идёт.</summary>
        public bool IsPointerOverUi(UnityEngine.Vector2 screenPosition) => _guard.IsOver(screenPosition);

        /// <summary>Курсор в видимом поле ввода: горячие клавиши (движение) молчат (CONTROLS §4).</summary>
        public bool IsTextInputFocused => TextInputFocus.IsEditing(_root.panel);

        public void Dispose()
        {
            _waitingCancel.clicked -= _screens.Cancel;
            _login.Dispose();
            _shards.Dispose();
            _characters.Dispose();
            _create.Dispose();
            _world.Dispose();
            _inspector.Dispose();
        }

        private void ShowOnly(LoginScreen screen)
        {
            UiElements.Show(_login.Panel, screen == LoginScreen.Login);
            UiElements.Show(_waiting, screen == LoginScreen.Waiting);
            UiElements.Show(_shards.Panel, screen == LoginScreen.Shards);
            UiElements.Show(_characters.Panel, screen == LoginScreen.Characters);
            UiElements.Show(_create.Panel, screen == LoginScreen.CreateCharacter);
            UiElements.Show(_world.Panel, screen == LoginScreen.InWorld);
            if (screen != LoginScreen.InWorld)
            {
                _inspector.Hide();
            }

            _root.EnableInClassList(InWorldClass, screen == LoginScreen.InWorld);
        }

        private void RefreshPanel(LoginScreen screen)
        {
            switch (screen)
            {
                case LoginScreen.Login:
                    _login.Refresh();
                    break;
                case LoginScreen.Waiting:
                    _waitingStatus.text = _screens.Status;
                    break;
                case LoginScreen.Shards:
                    _shards.Refresh();
                    break;
                case LoginScreen.Characters:
                    _characters.Refresh();
                    break;
                case LoginScreen.CreateCharacter:
                    _create.Refresh();
                    break;
                case LoginScreen.InWorld:
                    _world.Refresh();
                    break;
            }
        }
    }
}
