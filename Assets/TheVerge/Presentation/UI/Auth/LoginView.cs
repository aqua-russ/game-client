using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Экран авторизации (Экран 1) игры The Verge на базе TextMeshPro.
    /// Позволяет ввести логин и пароль, запомнить данные и отправить запрос входа.
    /// </summary>
    public sealed class LoginView
    {
        private const string PrefKeyAccount = "TheVerge_SavedAccount";
        private const string PrefKeyPassword = "TheVerge_SavedPassword";
        private const string PrefKeyRemember = "TheVerge_RememberMe";

        public GameObject RootObject { get; }
        public event Action<string, string> OnLoginRequested;

        private readonly TMP_InputField _accountInput;
        private readonly TMP_InputField _passwordInput;
        private readonly TextMeshProUGUI _rememberCheckboxText;
        private readonly TextMeshProUGUI _statusText;
        private readonly Button _loginBtn;
        private bool _rememberMe;

        public LoginView(Transform parent)
        {
            RootObject = new GameObject("LoginView", typeof(RectTransform));
            RootObject.transform.SetParent(parent, false);

            var panelRt = AuthUiTheme.CreatePanel(
                RootObject.transform,
                "LoginDialog",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(420f, 440f),
                AuthUiTheme.BgDark);

            BuildHeader(panelRt);

            string savedAccount = PlayerPrefs.GetString(PrefKeyAccount, "admin");
            string savedPassword = PlayerPrefs.GetString(PrefKeyPassword, "admin");
            _rememberMe = PlayerPrefs.GetInt(PrefKeyRemember, 1) == 1;

            var formContainer = new GameObject("Form", typeof(RectTransform));
            formContainer.transform.SetParent(panelRt, false);
            var formRt = formContainer.GetComponent<RectTransform>();
            formRt.anchoredPosition = new Vector2(0f, -30f);

            CreateLabel(formRt, "Логин аккаунта:", new Vector2(-170f, 60f));
            var (accObj, accInput) = AuthUiTheme.CreateInputField(formRt, "AccountInput", "Введите логин...", savedAccount, new Vector2(340f, 38f));
            accObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 30f);
            _accountInput = accInput;

            CreateLabel(formRt, "Пароль:", new Vector2(-170f, -5f));
            var (pwdObj, pwdInput) = AuthUiTheme.CreateInputField(formRt, "PasswordInput", "Введите пароль...", savedPassword, new Vector2(340f, 38f), TMP_InputField.ContentType.Password);
            pwdObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -35f);
            _passwordInput = pwdInput;

            var (remObj, remBtn) = AuthUiTheme.CreateButton(formRt, "RememberBtn", "", new Vector2(340f, 26f), Color.clear, AuthUiTheme.TextMuted, ToggleRememberMe);
            remObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -75f);
            _rememberCheckboxText = remObj.GetComponentInChildren<TextMeshProUGUI>();
            if (_rememberCheckboxText != null) _rememberCheckboxText.alignment = TextAlignmentOptions.Left;
            UpdateRememberText();

            var (btnObj, loginBtn) = AuthUiTheme.CreateButton(formRt, "LoginSubmitBtn", "ВОЙТИ В ИГРУ", new Vector2(340f, 44f), AuthUiTheme.PrimaryBtn, Color.white, HandleSubmit);
            btnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -125f);
            _loginBtn = loginBtn;

            _statusText = AuthUiTheme.CreateText(panelRt, "StatusText", "Готов к подключению к серверу The Verge", 11f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);
            var statusRt = _statusText.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 0.5f);
            statusRt.anchorMax = new Vector2(0.5f, 0.5f);
            statusRt.pivot = new Vector2(0.5f, 0.5f);
            statusRt.anchoredPosition = new Vector2(0f, -195f);
            statusRt.sizeDelta = new Vector2(380f, 32f);
        }

        private static void BuildHeader(Transform parent)
        {
            var headerRt = AuthUiTheme.CreatePanel(parent, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 80f), AuthUiTheme.PanelHeader);
            var title = AuthUiTheme.CreateText(headerRt, "Title", "THE VERGE", 22f, FontStyles.Bold, AuthUiTheme.GoldAccent, TextAlignmentOptions.Center);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0f, 14f);

            var subtitle = AuthUiTheme.CreateText(headerRt, "Subtitle", "Классическая 2D-изометрическая MMORPG", 11f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);
            var subRt = subtitle.GetComponent<RectTransform>();
            subRt.anchoredPosition = new Vector2(0f, -14f);
        }

        private static void CreateLabel(Transform parent, string text, Vector2 pos)
        {
            var txt = AuthUiTheme.CreateText(parent, "Label", text, 11f, FontStyles.Bold, AuthUiTheme.TextMuted, TextAlignmentOptions.Left);
            var rt = txt.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(340f, 20f);
        }

        private void ToggleRememberMe()
        {
            _rememberMe = !_rememberMe;
            UpdateRememberText();
        }

        private void UpdateRememberText()
        {
            if (_rememberCheckboxText == null) return;
            string check = _rememberMe ? "☑" : "☐";
            _rememberCheckboxText.text = $"{check} Запомнить логин и пароль";
            _rememberCheckboxText.color = _rememberMe ? AuthUiTheme.GoldAccent : AuthUiTheme.TextMuted;
        }

        private void HandleSubmit()
        {
            string acc = _accountInput.text.Trim();
            string pwd = _passwordInput.text;
            if (string.IsNullOrEmpty(acc))
            {
                SetStatus("Логин не может быть пустым!", isError: true);
                return;
            }

            if (_rememberMe)
            {
                PlayerPrefs.SetString(PrefKeyAccount, acc);
                PlayerPrefs.SetString(PrefKeyPassword, pwd);
                PlayerPrefs.SetInt(PrefKeyRemember, 1);
            }
            else
            {
                PlayerPrefs.DeleteKey(PrefKeyAccount);
                PlayerPrefs.DeleteKey(PrefKeyPassword);
                PlayerPrefs.SetInt(PrefKeyRemember, 0);
            }
            PlayerPrefs.Save();

            SetStatus("Авторизация на сервере...", isError: false);
            _loginBtn.interactable = false;
            OnLoginRequested?.Invoke(acc, pwd);
        }

        public void SetStatus(string message, bool isError)
        {
            if (_statusText == null) return;
            _statusText.text = message;
            _statusText.color = isError ? AuthUiTheme.ErrorRed : AuthUiTheme.TextMuted;
            _loginBtn.interactable = true;
        }

        public void Show() => RootObject.SetActive(true);
        public void Hide() => RootObject.SetActive(false);
    }
}
