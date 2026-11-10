using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Модальное окно внутриигрового меню выхода (ESC Menu) на базе TextMeshPro:
    /// - Возврат в меню выбора персонажа
    /// - Полный выход из игры (на рабочий стол)
    /// - Возврат в текущую игровую сессию
    /// </summary>
    public sealed class InGameEscapeMenuModal : MonoBehaviour
    {
        private GameObject _modalRoot;
        private Action _onReturnToCharSelect;
        private Action _onQuitGame;

        public bool IsOpen => _modalRoot != null && _modalRoot.activeSelf;

        public void Initialize(Transform canvasTransform, Action onReturnToCharSelect, Action onQuitGame)
        {
            _onReturnToCharSelect = onReturnToCharSelect;
            _onQuitGame = onQuitGame;

            BuildModal(canvasTransform);
            Hide();
        }

        private void BuildModal(Transform canvasTransform)
        {
            // Корневой полупрозрачный оверлей на весь экран
            _modalRoot = new GameObject("InGameEscapeMenuModal", typeof(RectTransform), typeof(Image));
            _modalRoot.transform.SetParent(canvasTransform, false);

            var rootRt = _modalRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = _modalRoot.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.65f);

            // Центральная карточка меню
            var cardObj = new GameObject("MenuCard", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(_modalRoot.transform, false);

            var cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(380f, 320f);
            cardRt.anchoredPosition = Vector2.zero;

            var cardImg = cardObj.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.10f, 0.14f, 0.98f);

            // Заголовок
            var title = CreateText(cardObj.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(340f, 30f), 18f, FontStyles.Bold, new Color(0.98f, 0.85f, 0.45f));
            title.alignment = TextAlignmentOptions.Center;
            title.text = "МЕНЮ ИГРЫ";

            // Подзаголовок
            var subtitle = CreateText(cardObj.transform, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(340f, 20f), 11f, FontStyles.Italic, new Color(0.75f, 0.80f, 0.88f));
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.text = "The Verge: Мир первозданных преданий";

            // Кнопка 1: Вернуться в игру
            CreateButton(cardObj.transform, "BtnResume", new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(320f, 46f),
                new Color(0.18f, 0.28f, 0.22f, 0.98f), "⚔ Вернуться в игру", Hide);

            // Кнопка 2: Выход в выбор персонажа
            CreateButton(cardObj.transform, "BtnCharSelect", new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(320f, 46f),
                new Color(0.18f, 0.28f, 0.42f, 0.98f), "📜 Выбор персонажа", () =>
                {
                    Hide();
                    _onReturnToCharSelect?.Invoke();
                });

            // Кнопка 3: Выход из игры
            CreateButton(cardObj.transform, "BtnQuit", new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(320f, 46f),
                new Color(0.45f, 0.16f, 0.16f, 0.98f), "🚪 Выход из игры", () =>
                {
                    Hide();
                    _onQuitGame?.Invoke();
                });

            // Подсказка снизу
            var hint = CreateText(cardObj.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(340f, 20f), 10f, FontStyles.Normal, new Color(0.55f, 0.58f, 0.65f));
            hint.alignment = TextAlignmentOptions.Center;
            hint.text = "[Esc] — закрыть меню и продолжить";
        }

        public void Show()
        {
            if (_modalRoot != null) _modalRoot.SetActive(true);
        }

        public void Hide()
        {
            if (_modalRoot != null) _modalRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txt = obj.GetComponent<TextMeshProUGUI>();
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color bgColor, string label, Action onClick)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = obj.GetComponent<Image>();
            img.color = bgColor;

            var btn = obj.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txt = CreateText(obj.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, size, 13f, FontStyles.Bold, Color.white);
            txt.alignment = TextAlignmentOptions.Center;
            txt.text = label;
            return btn;
        }
    }
}
