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
            if (screen == LoginScreen.CreateCharacter && _shownScreen != LoginScreen.CreateCharacter)
            {
                _create.Load();
            }

            _shownScreen = screen;
            ShowOnly(screen);
            RefreshPanel(screen);
        }

        public void Dispose()
        {
            _waitingCancel.clicked -= _screens.Cancel;
            _login.Dispose();
            _shards.Dispose();
            _characters.Dispose();
            _create.Dispose();
            _world.Dispose();
        }

        private void ShowOnly(LoginScreen screen)
        {
            UiElements.Show(_login.Panel, screen == LoginScreen.Login);
            UiElements.Show(_waiting, screen == LoginScreen.Waiting);
            UiElements.Show(_shards.Panel, screen == LoginScreen.Shards);
            UiElements.Show(_characters.Panel, screen == LoginScreen.Characters);
            UiElements.Show(_create.Panel, screen == LoginScreen.CreateCharacter);
            UiElements.Show(_world.Panel, screen == LoginScreen.InWorld);
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
