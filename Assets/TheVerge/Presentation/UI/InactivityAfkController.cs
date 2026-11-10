using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Контроллер обнаружения бездействия (AFK Timeout):
    /// Отслеживает пользовательский ввод (клавиатура, мышь).
    /// При отсутствии активности предупреждает за 30 сек и отключает от сервера через 5 минут.
    /// </summary>
    public sealed class InactivityAfkController : MonoBehaviour
    {
        [Header("Настройки времени AFK")]
        [SerializeField] private float _afkTimeoutSeconds = 600f; // 10 минут бездействия в игровом мире
        [SerializeField] private float _warningWindowSeconds = 60f; // Предупреждение за 60 сек

        private Action _onAfkTimeout;
        private Func<bool> _isConnectedCheck;
        private float _lastActivityTime;
        private bool _isWarningActive;

        private GameObject _warningBannerObj;
        private TextMeshProUGUI _warningCountdownText;

        public float RemainingSeconds => Mathf.Max(0f, _afkTimeoutSeconds - (Time.unscaledTime - _lastActivityTime));

        public void Initialize(Action onAfkTimeout, Func<bool> isConnectedCheck = null)
        {
            _onAfkTimeout = onAfkTimeout;
            _isConnectedCheck = isConnectedCheck;
            _lastActivityTime = Time.unscaledTime;

            BuildWarningBanner();
        }

        public void ResetTimer()
        {
            _lastActivityTime = Time.unscaledTime;
            if (_isWarningActive)
            {
                SetWarningVisible(false);
            }
        }

        private void Update()
        {
            // Проверяем активность только если есть активное подключение к серверу
            if (_isConnectedCheck != null && !_isConnectedCheck())
            {
                if (_isWarningActive) SetWarningVisible(false);
                _lastActivityTime = Time.unscaledTime;
                return;
            }

            if (CheckAnyUserInput())
            {
                ResetTimer();
                return;
            }

            float idleTime = Time.unscaledTime - _lastActivityTime;
            float remaining = _afkTimeoutSeconds - idleTime;

            if (remaining <= 0f)
            {
                SetWarningVisible(false);
                _lastActivityTime = Time.unscaledTime;
                _onAfkTimeout?.Invoke();
            }
            else if (remaining <= _warningWindowSeconds)
            {
                SetWarningVisible(true);
                int secs = Mathf.CeilToInt(remaining);
                if (_warningCountdownText != null)
                {
                    _warningCountdownText.text = $"⚠️ ВНИМАНИЕ: Отключение за неактивность через {secs} сек!\nНажмите любую клавишу или сдвиньте мышь.";
                }
            }
            else if (_isWarningActive)
            {
                SetWarningVisible(false);
            }
        }

        private static bool CheckAnyUserInput()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame ||
                    Mouse.current.rightButton.wasPressedThisFrame ||
                    Mouse.current.middleButton.wasPressedThisFrame)
                {
                    return true;
                }

                if (Mouse.current.delta.ReadValue().sqrMagnitude > 4.0f)
                {
                    return true;
                }

                if (Mouse.current.scroll.ReadValue().sqrMagnitude > 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildWarningBanner()
        {
            var canvasObj = new GameObject("AfkWarningCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Поверх всех интерфейсов

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);

            _warningBannerObj = new GameObject("AfkWarningBanner", typeof(RectTransform), typeof(Image));
            _warningBannerObj.transform.SetParent(canvasObj.transform, false);

            var rt = _warningBannerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -25f);
            rt.sizeDelta = new Vector2(560f, 64f);

            var img = _warningBannerObj.GetComponent<Image>();
            img.color = new Color(0.35f, 0.06f, 0.06f, 0.95f);

            var textObj = new GameObject("CountdownText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(_warningBannerObj.transform, false);

            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 4f);
            textRt.offsetMax = new Vector2(-12f, -4f);

            _warningCountdownText = textObj.GetComponent<TextMeshProUGUI>();
            _warningCountdownText.fontSize = 14f;
            _warningCountdownText.fontStyle = FontStyles.Bold;
            _warningCountdownText.color = new Color(1f, 0.92f, 0.40f);
            _warningCountdownText.alignment = TextAlignmentOptions.Center;
            _warningCountdownText.raycastTarget = false;

            _warningBannerObj.SetActive(false);
        }

        private void SetWarningVisible(bool visible)
        {
            _isWarningActive = visible;
            if (_warningBannerObj != null && _warningBannerObj.activeSelf != visible)
            {
                _warningBannerObj.SetActive(visible);
            }
        }
    }
}
