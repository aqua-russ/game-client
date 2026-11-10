using System;
using TheVerge.Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Экранный монитор сетевого статуса (Network HUD) на базе TextMeshPro.
    /// Отображает состояние подключения, пакеты и характеристики персонажа.
    /// </summary>
    public sealed class NetworkStatusHudPresenter : MonoBehaviour
    {
        private ModernUONetworkAdapter _networkAdapter;
        private TheVerge.Core.PlayerStatsModel _playerStats;
        private bool _isUsingSimulator;

        private GameObject _canvasObj;
        private RectTransform _panelRect;
        private GameObject _contentContainer;
        private TextMeshProUGUI _headerTitleText;
        private TextMeshProUGUI _collapseBtnText;
        private RectTransform _collapseBtnRect;
        private bool _isCollapsed = false;

        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _statsText;
        private RectTransform _rejectBtnRect;
        private float _nextUpdateTimer;

        public bool IsCollapsed => _isCollapsed;
        public event Action<bool> OnCollapseChanged;

        public void SetVisible(bool visible) => _canvasObj?.SetActive(visible);

        public void Initialize(ModernUONetworkAdapter networkAdapter, bool isUsingSimulator)
        {
            _networkAdapter = networkAdapter;
            _isUsingSimulator = isUsingSimulator;

            BuildUI();
        }

        public void UpdatePlayerStats(TheVerge.Core.PlayerStatsModel model)
        {
            _playerStats = model;
            UpdateView();
        }

        private void BuildUI()
        {
            var canvasObj = new GameObject("NetworkHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            _canvasObj = canvasObj;
            _canvasObj.SetActive(false);

            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);

            var panelObj = new GameObject("NetworkPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(canvasObj.transform, false);

            _panelRect = panelObj.GetComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0f, 1f);
            _panelRect.anchorMax = new Vector2(0f, 1f);
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.anchoredPosition = new Vector2(20f, -20f);
            _panelRect.sizeDelta = new Vector2(440f, 150f);

            var bg = panelObj.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.10f, 0.14f, 0.94f);

            var headerObj = new GameObject("NetworkHeader", typeof(RectTransform));
            headerObj.transform.SetParent(panelObj.transform, false);
            var headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 26f);

            _headerTitleText = CreateLabel(headerObj.transform, "HeaderTitle", 11f, FontStyles.Bold, new Color(0.35f, 0.90f, 0.45f), TextAlignmentOptions.Left);
            var htRect = _headerTitleText.GetComponent<RectTransform>();
            htRect.anchorMin = new Vector2(0f, 0f);
            htRect.anchorMax = new Vector2(1f, 1f);
            htRect.offsetMin = new Vector2(10f, 2f);
            htRect.offsetMax = new Vector2(-62f, -2f);
            _headerTitleText.text = "🟢 ModernUO :2593 | Сеть";

            var collapseBtnObj = new GameObject("CollapseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            collapseBtnObj.transform.SetParent(headerObj.transform, false);
            _collapseBtnRect = collapseBtnObj.GetComponent<RectTransform>();
            _collapseBtnRect.anchorMin = new Vector2(1f, 0.5f);
            _collapseBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _collapseBtnRect.pivot = new Vector2(1f, 0.5f);
            _collapseBtnRect.anchoredPosition = new Vector2(-6f, 0f);
            _collapseBtnRect.sizeDelta = new Vector2(52f, 20f);

            var collapseBtnImg = collapseBtnObj.GetComponent<Image>();
            collapseBtnImg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);

            _collapseBtnText = CreateLabel(collapseBtnObj.transform, "BtnText", 10f, FontStyles.Bold, new Color(0.85f, 0.90f, 0.95f), TextAlignmentOptions.Center);
            var cbtRect = _collapseBtnText.GetComponent<RectTransform>();
            cbtRect.anchorMin = Vector2.zero;
            cbtRect.anchorMax = Vector2.one;
            cbtRect.offsetMin = Vector2.zero;
            cbtRect.offsetMax = Vector2.zero;
            _collapseBtnText.text = "▲ Скрыть";

            var collapseBtn = collapseBtnObj.GetComponent<Button>();
            collapseBtn.onClick.AddListener(ToggleCollapse);

            _contentContainer = new GameObject("NetworkContent", typeof(RectTransform));
            _contentContainer.transform.SetParent(panelObj.transform, false);
            var contentRect = _contentContainer.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = new Vector2(0f, -26f);

            _statusText = CreateLabel(_contentContainer.transform, "StatusLabel", 11f, FontStyles.Normal, new Color(0.35f, 0.85f, 0.45f, 1f), TextAlignmentOptions.TopLeft);
            var statusRect = _statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(10f, -4f);
            statusRect.sizeDelta = new Vector2(-20f, 24f);

            _statsText = CreateLabel(_contentContainer.transform, "StatsLabel", 10f, FontStyles.Normal, new Color(0.85f, 0.88f, 0.92f, 1f), TextAlignmentOptions.TopLeft);
            var statsRect = _statsText.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0f, 1f);
            statsRect.anchorMax = new Vector2(1f, 1f);
            statsRect.pivot = new Vector2(0f, 1f);
            statsRect.anchoredPosition = new Vector2(10f, -28f);
            statsRect.sizeDelta = new Vector2(-20f, 56f);

            var btnObj = new GameObject("TestRejectButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(_contentContainer.transform, false);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(1f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 8f);
            btnRect.sizeDelta = new Vector2(-20f, 24f);

            var btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.24f, 0.28f, 0.36f, 0.95f);

            var btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(OnTestRejectClicked);
            _rejectBtnRect = btnRect;

            var btnText = CreateLabel(btnObj.transform, "BtnText", 10f, FontStyles.Normal, new Color(0.95f, 0.75f, 0.40f, 1f), TextAlignmentOptions.Center);
            var btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            btnText.text = "Тест отката: Имитировать 0x21 Move Reject";

            UpdateView();
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var txt = obj.GetComponent<TextMeshProUGUI>();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return txt;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                if (_canvasObj != null && !_canvasObj.activeSelf)
                {
                    _canvasObj.SetActive(true);
                    SetCollapsed(false);
                }
                else
                {
                    ToggleCollapse();
                }
            }

            if (_canvasObj != null && !_canvasObj.activeSelf) return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (_collapseBtnRect != null && _collapseBtnRect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(_collapseBtnRect, mousePos, null))
                {
                    ToggleCollapse();
                }
                else if (!_isCollapsed && _rejectBtnRect != null && _rejectBtnRect.gameObject.activeInHierarchy &&
                         RectTransformUtility.RectangleContainsScreenPoint(_rejectBtnRect, mousePos, null))
                {
                    OnTestRejectClicked();
                }
            }

            _nextUpdateTimer -= Time.deltaTime;
            if (_nextUpdateTimer <= 0f)
            {
                _nextUpdateTimer = 0.2f;
                UpdateView();
            }
        }

        public void ToggleCollapse()
        {
            SetCollapsed(!_isCollapsed);
        }

        public void SetCollapsed(bool collapsed)
        {
            _isCollapsed = collapsed;
            if (_contentContainer != null)
            {
                _contentContainer.SetActive(!_isCollapsed);
            }
            if (_panelRect != null)
            {
                _panelRect.sizeDelta = _isCollapsed ? new Vector2(440f, 26f) : new Vector2(440f, 150f);
            }
            if (_collapseBtnText != null)
            {
                _collapseBtnText.text = _isCollapsed ? "▼ Инфо" : "▲ Скрыть";
            }
            UpdateView();
            OnCollapseChanged?.Invoke(_isCollapsed);
        }

        private void UpdateView()
        {
            if (_headerTitleText == null) return;

            if (_isUsingSimulator || _networkAdapter == null)
            {
                if (_isCollapsed)
                {
                    _headerTitleText.text = "🔵 Оффлайн-симулятор | Сеть";
                }
                else
                {
                    _headerTitleText.text = "🔵 ModernUO Оффлайн-симулятор";
                    if (_statusText != null)
                    {
                        _statusText.text = "🔵 Режим: Оффлайн-симулятор мира (в памяти)";
                        _statusText.color = new Color(0.4f, 0.7f, 1f);
                    }
                    if (_statsText != null)
                    {
                        _statsText.text = "Локальная память: сокеты отключены.\nВ инспекторе выберите сеть для проверки сокетов.";
                    }
                }
                return;
            }

            string modePrefix = _networkAdapter.IsLiveServer
                ? "🟢 Живой ModernUO (2593)"
                : $"🟡 TCP-сервер ({_networkAdapter.ConnectedPort})";

            string statsSummary = _playerStats != null
                ? $"HP:{_playerStats.Hits}/{_playerStats.MaxHits} STAM:{_playerStats.Stamina}/{_playerStats.MaxStamina} MANA:{_playerStats.Mana}/{_playerStats.MaxMana} | Вес:{_playerStats.Weight}/{_playerStats.MaxWeight}"
                : $"TX: {_networkAdapter.SentPacketsCount} RX: {_networkAdapter.ReceivedPacketsCount} пак";

            if (_isCollapsed)
            {
                _headerTitleText.text = $"{modePrefix} | {statsSummary}";
                _headerTitleText.color = _networkAdapter.IsConnected
                    ? new Color(0.35f, 0.90f, 0.45f)
                    : new Color(0.95f, 0.45f, 0.35f);
            }
            else
            {
                _headerTitleText.text = $"{modePrefix} | Сеть";
                _headerTitleText.color = _networkAdapter.IsConnected
                    ? new Color(0.35f, 0.90f, 0.45f)
                    : new Color(0.95f, 0.45f, 0.35f);

                if (_statusText != null)
                {
                    string heroName = _playerStats != null && !string.IsNullOrEmpty(_playerStats.Name)
                        ? $" [{_playerStats.Name}]"
                        : string.Empty;
                    _statusText.text = $"{modePrefix}{heroName}\n{_networkAdapter.AuthStageDescription}";
                    _statusText.color = _networkAdapter.IsConnected
                        ? new Color(0.35f, 0.90f, 0.45f)
                        : new Color(0.95f, 0.45f, 0.35f);
                }

                if (_statsText != null)
                {
                    string statsLine = _playerStats != null
                        ? $"❤ HP: {_playerStats.Hits}/{_playerStats.MaxHits} | ⚡ Стамина: {_playerStats.Stamina}/{_playerStats.MaxStamina} | 💧 Мана: {_playerStats.Mana}/{_playerStats.MaxMana}\n" +
                          $"⚖ Вес: {_playerStats.Weight}/{_playerStats.MaxWeight} | STR: {_playerStats.Str} DEX: {_playerStats.Dex} INT: {_playerStats.Int} | 🪙 {_playerStats.Gold} зол. | 🛡 {_playerStats.ArmorRating} бр."
                        : "Ожидание пакета характеристик от сервера (0x11)...";

                    _statsText.text =
                        $"TX: {_networkAdapter.SentPacketsCount} | RX: {_networkAdapter.ReceivedPacketsCount} ({_networkAdapter.ReceivedBytesCount} B) | Seq: {_networkAdapter.LastSequenceKey}\n{statsLine}";
                }
            }
        }

        private void OnTestRejectClicked()
        {
            if (_networkAdapter != null)
            {
                _networkAdapter.SimulateMoveReject();
            }
        }
    }
}
