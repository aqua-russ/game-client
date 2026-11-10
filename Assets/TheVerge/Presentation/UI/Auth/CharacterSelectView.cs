using System;
using System.Collections.Generic;
using TheVerge.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Экран выбора персонажа (Экран 2) игры The Verge на базе TextMeshPro.
    /// Отображает слоты персонажей на аккаунте, позволяет войти существующим героем или создать нового.
    /// </summary>
    public sealed class CharacterSelectView
    {
        public GameObject RootObject { get; }

        public event Action<int> OnSelectCharacterRequested;
        public event Action<int> OnCreateCharacterRequested;
        public event Action<int> OnDeleteCharacterRequested;
        public event Action OnChangeAccountRequested;

        private readonly Transform _slotsContainer;
        private readonly TextMeshProUGUI _statusText;
        private readonly List<GameObject> _slotCards = new List<GameObject>();
        private CharacterListDto _lastCharacterList;

        public CharacterSelectView(Transform parent)
        {
            RootObject = new GameObject("CharacterSelectView", typeof(RectTransform));
            RootObject.transform.SetParent(parent, false);

            var panelRt = AuthUiTheme.CreatePanel(
                RootObject.transform,
                "SelectDialog",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 620f),
                AuthUiTheme.BgDark);

            BuildHeader(panelRt);

            var containerObj = new GameObject("SlotsContainer", typeof(RectTransform));
            containerObj.transform.SetParent(panelRt, false);
            var containerRt = containerObj.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = new Vector2(0f, -15f);
            containerRt.sizeDelta = new Vector2(500f, 420f);
            _slotsContainer = containerObj.transform;

            var bottomRt = AuthUiTheme.CreatePanel(panelRt, "Footer", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 60f), AuthUiTheme.PanelHeader);

            var (changeAccObj, _) = AuthUiTheme.CreateButton(bottomRt, "ChangeAccountBtn", "← Сменить аккаунт", new Vector2(180f, 36f), AuthUiTheme.SecondaryBtn, AuthUiTheme.TextPrimary, () => OnChangeAccountRequested?.Invoke());
            var changeRt = changeAccObj.GetComponent<RectTransform>();
            changeRt.anchorMin = new Vector2(0f, 0.5f);
            changeRt.anchorMax = new Vector2(0f, 0.5f);
            changeRt.pivot = new Vector2(0f, 0.5f);
            changeRt.anchoredPosition = new Vector2(20f, 0f);

            _statusText = AuthUiTheme.CreateText(bottomRt, "StatusText", "", 11f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Right);
            var statusRt = _statusText.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 0.5f);
            statusRt.anchorMax = new Vector2(1f, 0.5f);
            statusRt.pivot = new Vector2(1f, 0.5f);
            statusRt.offsetMin = new Vector2(210f, -18f);
            statusRt.offsetMax = new Vector2(-20f, 18f);
        }

        private static void BuildHeader(Transform parent)
        {
            var headerRt = AuthUiTheme.CreatePanel(parent, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 80f), AuthUiTheme.PanelHeader);
            var title = AuthUiTheme.CreateText(headerRt, "Title", "ЭКРАН ВЫБОРА ПЕРСОНАЖА", 20f, FontStyles.Bold, AuthUiTheme.GoldAccent, TextAlignmentOptions.Center);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0f, 14f);

            var subtitle = AuthUiTheme.CreateText(headerRt, "Subtitle", "Выберите персонажа для входа в мир The Verge или создайте нового", 11f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);
            var subRt = subtitle.GetComponent<RectTransform>();
            subRt.anchoredPosition = new Vector2(0f, -14f);
        }

        public void PopulateSlots(CharacterListDto characterList)
        {
            _lastCharacterList = characterList;
            foreach (var card in _slotCards) UnityEngine.Object.Destroy(card);
            _slotCards.Clear();

            int count = characterList.Slots != null && characterList.Slots.Count > 0 ? characterList.Slots.Count : 5;
            for (int i = 0; i < count; i++)
            {
                int slotIndex = i;
                var slotDto = (characterList.Slots != null && i < characterList.Slots.Count) ? characterList.Slots[i] : new CharacterSlotDto(i, string.Empty);
                float yPos = 160f - i * 78f;

                var cardRt = AuthUiTheme.CreatePanel(
                    _slotsContainer,
                    $"SlotCard_{slotIndex}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, yPos), new Vector2(490f, 68f),
                    !slotDto.IsEmpty ? AuthUiTheme.CardBg : new Color(0.10f, 0.12f, 0.16f, 0.70f));

                _slotCards.Add(cardRt.gameObject);

                if (!slotDto.IsEmpty)
                {
                    var nameText = AuthUiTheme.CreateText(cardRt, "CharName", $"⚔  {slotDto.Name}", 15f, FontStyles.Bold, AuthUiTheme.TextPrimary, TextAlignmentOptions.Left);
                    var nameRt = nameText.GetComponent<RectTransform>();
                    nameRt.offsetMin = new Vector2(16f, 10f);
                    nameRt.offsetMax = new Vector2(-235f, 0f);

                    var descText = AuthUiTheme.CreateText(cardRt, "SlotInfo", $"Слот #{slotIndex + 1} • Готов к приключениям", 10f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Left);
                    var descRt = descText.GetComponent<RectTransform>();
                    descRt.offsetMin = new Vector2(16f, -15f);
                    descRt.offsetMax = new Vector2(-235f, -25f);

                    var (playObj, _) = AuthUiTheme.CreateButton(cardRt, "PlayBtn", "ВОЙТИ В МИР", new Vector2(125f, 36f), AuthUiTheme.PrimaryBtn, Color.white, () => OnSelectCharacterRequested?.Invoke(slotIndex));
                    var playRt = playObj.GetComponent<RectTransform>();
                    playRt.anchorMin = new Vector2(1f, 0.5f);
                    playRt.anchorMax = new Vector2(1f, 0.5f);
                    playRt.pivot = new Vector2(1f, 0.5f);
                    playRt.anchoredPosition = new Vector2(-96f, 0f);

                    var (delObj, _) = AuthUiTheme.CreateButton(cardRt, "DelBtn", "🗑 Удалить", new Vector2(82f, 36f), AuthUiTheme.DangerBtn, Color.white, () => ShowDeleteConfirm(cardRt, slotIndex, slotDto.Name));
                    var delRt = delObj.GetComponent<RectTransform>();
                    delRt.anchorMin = new Vector2(1f, 0.5f);
                    delRt.anchorMax = new Vector2(1f, 0.5f);
                    delRt.pivot = new Vector2(1f, 0.5f);
                    delRt.anchoredPosition = new Vector2(-10f, 0f);
                }
                else
                {
                    var emptyText = AuthUiTheme.CreateText(cardRt, "EmptyLabel", $"[ Слот #{slotIndex + 1} пуст ]", 13f, FontStyles.Italic, AuthUiTheme.TextMuted, TextAlignmentOptions.Left);
                    var emptyRt = emptyText.GetComponent<RectTransform>();
                    emptyRt.offsetMin = new Vector2(18f, 0f);
                    emptyRt.offsetMax = new Vector2(-190f, 0f);

                    var (btnObj, _) = AuthUiTheme.CreateButton(cardRt, "CreateBtn", "+ Создать персонажа", new Vector2(170f, 36f), AuthUiTheme.SecondaryBtn, AuthUiTheme.GoldAccent, () => OnCreateCharacterRequested?.Invoke(slotIndex));
                    var btnRt = btnObj.GetComponent<RectTransform>();
                    btnRt.anchorMin = new Vector2(1f, 0.5f);
                    btnRt.anchorMax = new Vector2(1f, 0.5f);
                    btnRt.pivot = new Vector2(1f, 0.5f);
                    btnRt.anchoredPosition = new Vector2(-15f, 0f);
                }
            }
        }

        private void ShowDeleteConfirm(RectTransform cardRt, int slotIndex, string name)
        {
            for (int i = cardRt.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(cardRt.GetChild(i).gameObject);
            }

            var promptText = AuthUiTheme.CreateText(cardRt, "ConfirmPrompt", $"Точно удалить героя «{name}»?", 12f, FontStyles.Bold, AuthUiTheme.ErrorRed, TextAlignmentOptions.Left);
            var pRt = promptText.GetComponent<RectTransform>();
            pRt.offsetMin = new Vector2(16f, 0f);
            pRt.offsetMax = new Vector2(-210f, 0f);

            var (yesObj, _) = AuthUiTheme.CreateButton(cardRt, "ConfirmYes", "ДА, УДАЛИТЬ", new Vector2(110f, 34f), AuthUiTheme.DangerBtn, Color.white, () =>
            {
                SetStatus($"Удаление {name}...");
                OnDeleteCharacterRequested?.Invoke(slotIndex);
            });
            var yesRt = yesObj.GetComponent<RectTransform>();
            yesRt.anchorMin = new Vector2(1f, 0.5f);
            yesRt.anchorMax = new Vector2(1f, 0.5f);
            yesRt.pivot = new Vector2(1f, 0.5f);
            yesRt.anchoredPosition = new Vector2(-92f, 0f);

            var (noObj, _) = AuthUiTheme.CreateButton(cardRt, "ConfirmNo", "Отмена", new Vector2(76f, 34f), AuthUiTheme.SecondaryBtn, Color.white, () =>
            {
                PopulateSlots(_lastCharacterList);
            });
            var noRt = noObj.GetComponent<RectTransform>();
            noRt.anchorMin = new Vector2(1f, 0.5f);
            noRt.anchorMax = new Vector2(1f, 0.5f);
            noRt.pivot = new Vector2(1f, 0.5f);
            noRt.anchoredPosition = new Vector2(-10f, 0f);
        }

        public void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
        }

        public void Show() => RootObject.SetActive(true);
        public void Hide() => RootObject.SetActive(false);
    }
}
