using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Цветовая палитра и вспомогательные фабричные методы создания UI на базе TextMeshPro.
    /// Стиль: темный сланец с золотыми и бронзовыми акцентами (MMORPG).
    /// </summary>
    public static class AuthUiTheme
    {
        public static readonly Color BgDark = new Color(0.08f, 0.10f, 0.14f, 0.96f);
        public static readonly Color PanelHeader = new Color(0.12f, 0.15f, 0.21f, 1.0f);
        public static readonly Color CardBg = new Color(0.12f, 0.14f, 0.19f, 0.95f);
        public static readonly Color CardSelected = new Color(0.18f, 0.24f, 0.34f, 1.0f);
        public static readonly Color GoldAccent = new Color(0.96f, 0.80f, 0.38f, 1.0f);
        public static readonly Color TextPrimary = new Color(0.93f, 0.94f, 0.96f, 1.0f);
        public static readonly Color TextMuted = new Color(0.65f, 0.68f, 0.73f, 1.0f);
        public static readonly Color SuccessGreen = new Color(0.25f, 0.72f, 0.38f, 1.0f);
        public static readonly Color ErrorRed = new Color(0.88f, 0.26f, 0.26f, 1.0f);
        public static readonly Color PrimaryBtn = new Color(0.18f, 0.48f, 0.28f, 1.0f);
        public static readonly Color SecondaryBtn = new Color(0.20f, 0.25f, 0.33f, 1.0f);
        public static readonly Color DangerBtn = new Color(0.55f, 0.18f, 0.18f, 1.0f);
        public static readonly Color InputBg = new Color(0.06f, 0.07f, 0.10f, 0.95f);

        public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            obj.GetComponent<Image>().color = color;
            return rt;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var txt = obj.GetComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;

            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return txt;
        }

        public static (GameObject btnObj, Button btn) CreateButton(Transform parent, string name, string label, Vector2 size, Color bgColor, Color textColor, Action onClick)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            obj.GetComponent<Image>().color = bgColor;

            var btn = obj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            CreateText(obj.transform, "Label", label, 13f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            return (obj, btn);
        }

        public static (GameObject inputObj, TMP_InputField input) CreateInputField(Transform parent, string name, string placeholder, string defaultText, Vector2 size, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            obj.GetComponent<Image>().color = InputBg;

            var ph = CreateText(obj.transform, "Placeholder", placeholder, 13f, FontStyles.Italic, TextMuted, TextAlignmentOptions.Left);
            var phRt = ph.GetComponent<RectTransform>();
            phRt.offsetMin = new Vector2(10f, 0f);
            phRt.offsetMax = new Vector2(-10f, 0f);

            var val = CreateText(obj.transform, "ValueText", defaultText, 13f, FontStyles.Normal, TextPrimary, TextAlignmentOptions.Left);
            var valRt = val.GetComponent<RectTransform>();
            valRt.offsetMin = new Vector2(10f, 0f);
            valRt.offsetMax = new Vector2(-10f, 0f);

            var input = obj.GetComponent<TMP_InputField>();
            input.textViewport = rt;
            input.textComponent = val;
            input.placeholder = ph;
            input.contentType = contentType;
            input.text = defaultText;

            return (obj, input);
        }
    }
}
