using System;
using TheVerge.Core.Session.Screens;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>
    /// Экран входа: адрес, логин, пароль (поле скрыто звёздочками), подсказка, причина прошлой ошибки и «Войти»
    /// (Enter — то же). Значения сразу уходят в <see cref="LoginForm"/>; пароль никуда больше не пишется.
    /// </summary>
    public sealed class LoginPanelView : IDisposable
    {
        private readonly LoginScreens _screens;
        private readonly VisualElement _panel;
        private readonly TextField _address;
        private readonly TextField _username;
        private readonly TextField _password;
        private readonly Label _problem;
        private readonly Label _message;
        private readonly Button _login;

        public LoginPanelView(VisualElement root, LoginScreens screens)
        {
            _screens = screens;
            _panel = UiElements.Require<VisualElement>(root, "loginPanel");
            _address = UiElements.Require<TextField>(_panel, "addressField");
            _username = UiElements.Require<TextField>(_panel, "usernameField");
            _password = UiElements.Require<TextField>(_panel, "passwordField");
            _problem = UiElements.Require<Label>(_panel, "loginProblem");
            _message = UiElements.Require<Label>(_panel, "loginMessage");
            _login = UiElements.Require<Button>(_panel, "loginButton");
            _password.isPasswordField = true;
            _address.SetValueWithoutNotify(screens.Form.Address);
            _username.SetValueWithoutNotify(screens.Form.Username);
            _address.RegisterValueChangedCallback(OnAddress);
            _username.RegisterValueChangedCallback(OnUsername);
            _password.RegisterValueChangedCallback(OnPassword);
            _panel.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _login.clicked += _screens.Submit;
        }

        public VisualElement Panel => _panel;

        public void Refresh()
        {
            UiElements.SetText(_problem, _screens.Form.Problem);
            UiElements.SetText(_message, _screens.Message);
            _login.SetEnabled(_screens.Form.CanSubmit);
        }

        public void Dispose()
        {
            _address.UnregisterValueChangedCallback(OnAddress);
            _username.UnregisterValueChangedCallback(OnUsername);
            _password.UnregisterValueChangedCallback(OnPassword);
            _panel.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _login.clicked -= _screens.Submit;
        }

        private void OnAddress(ChangeEvent<string> e) => _screens.Form.Address = e.newValue;

        private void OnUsername(ChangeEvent<string> e) => _screens.Form.Username = e.newValue;

        private void OnPassword(ChangeEvent<string> e) => _screens.Form.Password = e.newValue;

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                _screens.Submit();
            }
        }
    }
}
