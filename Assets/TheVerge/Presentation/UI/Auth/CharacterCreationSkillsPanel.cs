using System;
using System.Collections.Generic;
using TheVerge.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Панель выбора навыков (Вариант Б) в создании персонажа на базе TextMeshPro.
    /// Пул 100 очков, максимум 3 навыка (слайдеры 0..50).
    /// Выбранные навыки поднимаются вверх и показывают стартовый набор.
    /// </summary>
    public sealed class CharacterCreationSkillsPanel
    {
        private const int MaxSkills = 3;
        private const int MaxSkillValue = 50;
        private const int TotalTargetPoints = 100;

        public Transform RootTransform { get; }
        public event Action OnSkillsChanged;

        private readonly List<(SkillType skill, int value)> _selected = new List<(SkillType, int)>();
        private readonly Transform _topSelectedContainer;
        private readonly Transform _bottomAvailableContainer;
        private readonly TextMeshProUGUI _counterText;
        private readonly List<GameObject> _topCards = new List<GameObject>();
        private readonly List<GameObject> _bottomButtons = new List<GameObject>();

        public IReadOnlyList<(SkillType skill, int value)> SelectedSkills => _selected;
        public int TotalPointsAllocated
        {
            get
            {
                int sum = 0;
                foreach (var s in _selected) sum += s.value;
                return sum;
            }
        }
        public bool IsValid => _selected.Count == MaxSkills && TotalPointsAllocated == TotalTargetPoints;

        public CharacterCreationSkillsPanel(Transform parent)
        {
            var panelRt = AuthUiTheme.CreatePanel(
                parent, "SkillsPanel",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.08f, 0.11f, 0.90f));
            RootTransform = panelRt;

            var topHeader = AuthUiTheme.CreatePanel(panelRt, "TopHeader", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 32f), AuthUiTheme.PanelHeader);
            _counterText = AuthUiTheme.CreateText(topHeader, "Counter", "", 12f, FontStyles.Bold, AuthUiTheme.GoldAccent, TextAlignmentOptions.Left);
            _counterText.GetComponent<RectTransform>().offsetMin = new Vector2(12f, 0f);

            var topObj = new GameObject("SelectedArea", typeof(RectTransform));
            topObj.transform.SetParent(panelRt, false);
            var topRt = topObj.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.anchoredPosition = new Vector2(0f, -34f);
            topRt.sizeDelta = new Vector2(0f, 170f);
            _topSelectedContainer = topObj.transform;

            var divider = AuthUiTheme.CreatePanel(panelRt, "Divider", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(-16f, 22f), AuthUiTheme.PanelHeader);
            AuthUiTheme.CreateText(divider, "DivText", "Доступные навыки (нажмите для выбора)", 10f, FontStyles.Bold, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);

            var scrollObj = new GameObject("ScrollArea", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollObj.transform.SetParent(panelRt, false);
            scrollObj.GetComponent<Image>().color = Color.clear;
            var scrollRt = scrollObj.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 0f);
            scrollRt.offsetMin = new Vector2(6f, 6f);
            scrollRt.offsetMax = new Vector2(-6f, -232f);

            var contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(scrollObj.transform, false);
            var contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 240f);
            _bottomAvailableContainer = contentObj.transform;

            var scroll = scrollObj.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20f;

            RefreshUI();
        }

        public void ApplyPreset(SkillType s1, int v1, SkillType s2, int v2, SkillType s3, int v3)
        {
            _selected.Clear();
            _selected.Add((s1, Mathf.Clamp(v1, 0, MaxSkillValue)));
            _selected.Add((s2, Mathf.Clamp(v2, 0, MaxSkillValue)));
            _selected.Add((s3, Mathf.Clamp(v3, 0, MaxSkillValue)));
            RefreshUI();
            OnSkillsChanged?.Invoke();
        }

        private void RefreshUI()
        {
            UpdateCounter();
            RenderSelectedArea();
            RenderAvailableArea();
        }

        private void UpdateCounter()
        {
            int current = TotalPointsAllocated;
            string status = current == TotalTargetPoints && _selected.Count == MaxSkills ? "✓ Пул распределен верно" : $"(нужно ровно {TotalTargetPoints})";
            _counterText.text = $"ВЫБРАННЫЕ НАВЫКИ: {_selected.Count}/{MaxSkills}   |   ОЧКИ НАВЫКОВ: {current}/{TotalTargetPoints} {status}";
            _counterText.color = (current == TotalTargetPoints && _selected.Count == MaxSkills) ? AuthUiTheme.SuccessGreen : AuthUiTheme.GoldAccent;
        }

        private void RenderSelectedArea()
        {
            foreach (var card in _topCards) UnityEngine.Object.Destroy(card);
            _topCards.Clear();

            for (int i = 0; i < MaxSkills; i++)
            {
                int index = i;
                float yPos = -6f - i * 54f;
                bool hasSkill = index < _selected.Count;

                var cardRt = AuthUiTheme.CreatePanel(
                    _topSelectedContainer, $"SelectedCard_{index}",
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, yPos), new Vector2(-12f, 50f),
                    hasSkill ? AuthUiTheme.CardBg : new Color(0.09f, 0.11f, 0.15f, 0.50f));
                _topCards.Add(cardRt.gameObject);

                if (hasSkill)
                {
                    var (skill, val) = _selected[index];
                    string kit = SkillInfo.GetStarterKitDescription(skill);
                    var title = AuthUiTheme.CreateText(cardRt, "SkillName", $"★ {SkillInfo.GetNameRu(skill)}", 12f, FontStyles.Bold, AuthUiTheme.GoldAccent, TextAlignmentOptions.Left);
                    title.GetComponent<RectTransform>().offsetMin = new Vector2(10f, 12f);

                    var kitText = AuthUiTheme.CreateText(cardRt, "Kit", $"Набор: {kit}", 9f, FontStyles.Italic, AuthUiTheme.TextMuted, TextAlignmentOptions.Left);
                    kitText.GetComponent<RectTransform>().offsetMin = new Vector2(10f, -14f);

                    CreateStepper(cardRt, val, delta => ModifySkillValue(index, delta));

                    var (rmObj, _) = AuthUiTheme.CreateButton(cardRt, "RemoveBtn", "✕", new Vector2(24f, 24f), AuthUiTheme.DangerBtn, Color.white, () => RemoveSkill(index));
                    var rmRt = rmObj.GetComponent<RectTransform>();
                    rmRt.anchorMin = new Vector2(1f, 0.5f);
                    rmRt.anchorMax = new Vector2(1f, 0.5f);
                    rmRt.pivot = new Vector2(1f, 0.5f);
                    rmRt.anchoredPosition = new Vector2(-8f, 0f);
                }
                else
                {
                    AuthUiTheme.CreateText(cardRt, "Empty", $"[ Слот #{index + 1} свободен — выберите умение из списка ниже ]", 11f, FontStyles.Italic, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);
                }
            }
        }

        private void CreateStepper(Transform parent, int val, Action<int> onDelta)
        {
            var stepperObj = new GameObject("Stepper", typeof(RectTransform));
            stepperObj.transform.SetParent(parent, false);
            var rt = stepperObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-40f, 0f);
            rt.sizeDelta = new Vector2(160f, 30f);

            var (m10Obj, _) = AuthUiTheme.CreateButton(stepperObj.transform, "M10", "-10", new Vector2(28f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(-10));
            m10Obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-60f, 0f);

            var (m1Obj, _) = AuthUiTheme.CreateButton(stepperObj.transform, "M1", "-", new Vector2(24f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(-1));
            m1Obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-32f, 0f);

            var valTxt = AuthUiTheme.CreateText(stepperObj.transform, "Val", val.ToString(), 13f, FontStyles.Bold, AuthUiTheme.TextPrimary, TextAlignmentOptions.Center);
            valTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(36f, 24f);

            var (p1Obj, _) = AuthUiTheme.CreateButton(stepperObj.transform, "P1", "+", new Vector2(24f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(1));
            p1Obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(32f, 0f);

            var (p10Obj, _) = AuthUiTheme.CreateButton(stepperObj.transform, "P10", "+10", new Vector2(28f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(10));
            p10Obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(60f, 0f);
        }

        private void RenderAvailableArea()
        {
            foreach (var b in _bottomButtons) UnityEngine.Object.Destroy(b);
            _bottomButtons.Clear();

            var selectedSet = new HashSet<SkillType>();
            foreach (var s in _selected) selectedSet.Add(s.skill);

            int col = 0;
            int row = 0;
            const float colWidth = 145f;
            const float rowHeight = 28f;

            foreach (var entry in SkillInfo.AllSkills)
            {
                if (selectedSet.Contains(entry.Type)) continue;
                var skillRef = entry.Type;

                var (btnObj, _) = AuthUiTheme.CreateButton(
                    _bottomAvailableContainer, $"Avail_{entry.Type}",
                    entry.NameRu,
                    new Vector2(colWidth, rowHeight),
                    AuthUiTheme.CardBg, AuthUiTheme.TextMuted,
                    () => PickAvailableSkill(skillRef));

                var rt = btnObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(col * (colWidth + 4f) + 6f, -row * (rowHeight + 4f) - 4f);

                _bottomButtons.Add(btnObj);
                col++;
                if (col >= 3)
                {
                    col = 0;
                    row++;
                }
            }
        }

        private void PickAvailableSkill(SkillType skill)
        {
            if (_selected.Count >= MaxSkills) return;
            int remainingPool = Mathf.Max(0, TotalTargetPoints - TotalPointsAllocated);
            int startVal = Mathf.Min(remainingPool, MaxSkillValue);
            _selected.Add((skill, startVal));
            RefreshUI();
            OnSkillsChanged?.Invoke();
        }

        private void RemoveSkill(int index)
        {
            if (index >= 0 && index < _selected.Count)
            {
                _selected.RemoveAt(index);
                RefreshUI();
                OnSkillsChanged?.Invoke();
            }
        }

        private void ModifySkillValue(int index, int delta)
        {
            if (index < 0 || index >= _selected.Count) return;
            int currentVal = _selected[index].value;
            int currentTotal = TotalPointsAllocated;
            int targetVal = Mathf.Clamp(currentVal + delta, 0, MaxSkillValue);

            int netChange = targetVal - currentVal;
            if (currentTotal + netChange > TotalTargetPoints)
            {
                targetVal = currentVal + (TotalTargetPoints - currentTotal);
            }

            _selected[index] = (_selected[index].skill, targetVal);
            RefreshUI();
            OnSkillsChanged?.Invoke();
        }
    }
}
