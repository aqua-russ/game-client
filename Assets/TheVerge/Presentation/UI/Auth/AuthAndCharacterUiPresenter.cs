using System;
using TheVerge.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Главный презентер экранов авторизации и создания персонажа (Шаг 1.0 GDD The Verge):
    /// 1. Экран авторизации (LoginView)
    /// 2. Экран выбора персонажа (CharacterSelectView)
    /// 3. Экран создания персонажа (CharacterCreationView)
    /// </summary>
    public sealed class AuthAndCharacterUiPresenter : MonoBehaviour
    {
        private ISessionAdapter _session;
        private string _serverHost;
        private int _serverPort;
        private string _currentAccount = "test";
        private string _currentPassword = "test";
        private bool _isIntentionalReturnToSelect;
        private bool _isAfkDisconnected;

        private GameObject _canvasObj;
        private LoginView _loginView;
        private CharacterSelectView _selectView;
        private CharacterCreationView _creationView;
        private CharacterListDto? _lastCharacterList;

        public event Action OnWorldResetRequested;

        public void Initialize(ISessionAdapter session, string serverHost, int serverPort)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _serverHost = serverHost;
            _serverPort = serverPort;

            EnsureEventSystem();
            BuildCanvas();

            _session.OnCharacterListReceived += HandleCharacterListReceived;
            _session.OnCharacterDeleteFailed += HandleCharacterDeleteFailed;
            _session.OnPlayerEnteredWorld += HandlePlayerEnteredWorld;
            _session.OnSessionError += HandleSessionError;
            _session.OnDisconnected += HandleDisconnected;

            SetGameplayHudsActive(false);
            ShowLogin();
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(esObj);
            }
        }

        private void BuildCanvas()
        {
            _canvasObj = new GameObject("AuthCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObj.transform.SetParent(transform, false);

            var canvas = _canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = _canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);

            // Сплошной темный фон, закрывающий 3D-мир до входа в игру
            var backdropRt = AuthUiTheme.CreatePanel(
                _canvasObj.transform, "FullscreenBackdrop",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.05f, 0.07f, 1.0f));
            backdropRt.offsetMin = Vector2.zero;
            backdropRt.offsetMax = Vector2.zero;
            backdropRt.SetAsFirstSibling();

            _loginView = new LoginView(_canvasObj.transform);
            _loginView.OnLoginRequested += HandleLoginRequested;

            _selectView = new CharacterSelectView(_canvasObj.transform);
            _selectView.OnSelectCharacterRequested += HandleSelectCharacterRequested;
            _selectView.OnCreateCharacterRequested += HandleOpenCreationRequested;
            _selectView.OnDeleteCharacterRequested += HandleDeleteCharacterRequested;
            _selectView.OnChangeAccountRequested += ShowLogin;

            _creationView = new CharacterCreationView(_canvasObj.transform);
            _creationView.OnCreateCharacterSubmitted += HandleCreateCharacterSubmitted;
            _creationView.OnBackToSelectRequested += ShowSelect;
        }

        private void ShowLogin()
        {
            _loginView.Show();
            _selectView.Hide();
            _creationView.Hide();
        }

        private void ShowSelect()
        {
            _loginView.Hide();
            _selectView.Show();
            _creationView.Hide();
        }

        private void ShowCreation(int slotIndex)
        {
            _loginView.Hide();
            _selectView.Hide();
            _creationView.PrepareForSlot(slotIndex);
            _creationView.Show();
        }

        private void HandleLoginRequested(string account, string password)
        {
            _currentAccount = account;
            _currentPassword = password;
            _loginView.SetStatus($"Подключение к {_serverHost}:{_serverPort}...", isError: false);
            _session.Login(_serverHost, _serverPort, account, password);
        }

        private void HandleCharacterListReceived(CharacterListDto list)
        {
            _lastCharacterList = list;
            _isIntentionalReturnToSelect = false;
            _selectView.PopulateSlots(list);
            ShowSelect();
        }

        private void HandleSelectCharacterRequested(int slotIndex)
        {
            _selectView.SetStatus($"Вход в мир персонажем из слота #{slotIndex + 1}...");
            string charName = _lastCharacterList.HasValue && _lastCharacterList.Value.Slots != null && slotIndex >= 0 && slotIndex < _lastCharacterList.Value.Slots.Count
                ? _lastCharacterList.Value.Slots[slotIndex].Name
                : null;
            _session.SelectCharacter(slotIndex, charName);
        }

        private void HandleOpenCreationRequested(int slotIndex)
        {
            ShowCreation(slotIndex);
        }

        private void HandleDeleteCharacterRequested(int slotIndex)
        {
            _session.DeleteCharacter(slotIndex);
        }

        private void HandleCharacterDeleteFailed(string reason)
        {
            _selectView.SetStatus($"Ошибка удаления: {reason}");
        }

        private void HandleCreateCharacterSubmitted(CharacterCreationArgsDto args, int slotIndex)
        {
            _session.CreateCharacter(args, slotIndex);
        }

        private void HandlePlayerEnteredWorld(PlayerLoginConfirmDto confirm)
        {
            Debug.Log($"[AuthPresenter] 🟢 Игрок вошел в мир! Serial={confirm.PlayerSerial}. Скрываем UI авторизации.");
            _canvasObj.SetActive(false);
            SetGameplayHudsActive(true);
        }

        private void HandleSessionError(string error)
        {
            Debug.LogError($"[AuthPresenter] 🔴 Ошибка сессии: {error}");
            if (_loginView.RootObject.activeSelf) _loginView.SetStatus($"Ошибка: {error}", isError: true);
            else if (_selectView.RootObject.activeSelf) _selectView.SetStatus($"Ошибка: {error}");
        }

        private void HandleDisconnected()
        {
            if (_isIntentionalReturnToSelect)
            {
                return;
            }

            Debug.LogWarning("[AuthPresenter] 🟡 Сессия завершена. Возвращаемся на экран входа.");
            SetGameplayHudsActive(false);
            _canvasObj.SetActive(true);
            ShowLogin();

            if (_isAfkDisconnected)
            {
                _isAfkDisconnected = false;
                _loginView.SetStatus("Вы были отключены от сервера из-за бездействия (AFK)", isError: true);
            }
            else
            {
                _loginView.SetStatus("Соединение с сервером разорвано", isError: true);
            }
        }

        /// <summary>
        /// Выход из игрового мира обратно в меню выбора персонажей (без ввода пароля заново).
        /// </summary>
        public async void ReturnToCharacterSelect()
        {
            Debug.Log("[AuthPresenter] 🔄 Возврат в меню выбора персонажа...");
            _isIntentionalReturnToSelect = true;

            OnWorldResetRequested?.Invoke();
            SetGameplayHudsActive(false);
            _canvasObj.SetActive(true);
            _selectView.SetStatus("Загрузка списка персонажей...");
            ShowSelect();

            _session.Disconnect();
            await System.Threading.Tasks.Task.Delay(180);

            if (!string.IsNullOrEmpty(_currentAccount))
            {
                _session.Login(_serverHost, _serverPort, _currentAccount, _currentPassword);
            }
            else
            {
                _isIntentionalReturnToSelect = false;
                ShowLogin();
            }
        }

        /// <summary>
        /// Полное завершение игры.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[AuthPresenter] 🚪 Завершение игры...");
            _session?.Disconnect();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Вызывается при срабатывании тайм-аута бездействия (AFK).
        /// </summary>
        public void HandleAfkTimeout()
        {
            Debug.LogWarning("[AuthPresenter] ⏳ Сработал тайм-аут бездействия (AFK)!");
            _isAfkDisconnected = true;
            _isIntentionalReturnToSelect = false;

            OnWorldResetRequested?.Invoke();
            SetGameplayHudsActive(false);
            _session.Disconnect();

            _canvasObj.SetActive(true);
            ShowLogin();
            _loginView.SetStatus("Вы были отключены от сервера из-за бездействия (AFK)", isError: true);
        }

        private static void SetGameplayHudsActive(bool active)
        {
            var netHud = FindAnyObjectByType<NetworkStatusHudPresenter>();
            if (netHud != null) netHud.SetVisible(active); // Сетевой монитор с характеристиками (Шаг 1.2)
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnCharacterListReceived -= HandleCharacterListReceived;
                _session.OnCharacterDeleteFailed -= HandleCharacterDeleteFailed;
                _session.OnPlayerEnteredWorld -= HandlePlayerEnteredWorld;
                _session.OnSessionError -= HandleSessionError;
                _session.OnDisconnected -= HandleDisconnected;
            }
        }
    }
}
