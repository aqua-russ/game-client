using System;
using TheVerge.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheVerge.Presentation
{
    /// <summary>
    /// Экран создания персонажа (Экран 3) игры The Verge на базе TextMeshPro.
    /// Включает ввод имени, выбор пола, пресеты, распределение характеристик (80 очков)
    /// и выбор 3 навыков (100 очков) по Варианту Б.
    /// </summary>
    public sealed class CharacterCreationView
    {
        private const int TotalTargetStats = 80;
        private const int MinStat = 10;
        private const int MaxStat = 60;

        public GameObject RootObject { get; }
        public event Action<CharacterCreationArgsDto, int> OnCreateCharacterSubmitted;
        public event Action OnBackToSelectRequested;

        private int _targetSlotIndex = 0;
        private readonly TMP_InputField _nameInput;
        private readonly TextMeshProUGUI _genderBtnText;
        private bool _isFemale = false;

        private int _str = 45;
        private int _dex = 20;
        private int _int = 15;
        private readonly TextMeshProUGUI _strValText;
        private readonly TextMeshProUGUI _dexValText;
        private readonly TextMeshProUGUI _intValText;
        private readonly TextMeshProUGUI _statsCounterText;

        private readonly CharacterCreationSkillsPanel _skillsPanel;
        private readonly Button _submitBtn;
        private readonly TextMeshProUGUI _validationText;

        public CharacterCreationView(Transform parent)
        {
            RootObject = new GameObject("CharacterCreationView", typeof(RectTransform));
            RootObject.transform.SetParent(parent, false);

            var panelRt = AuthUiTheme.CreatePanel(
                RootObject.transform,
                "CreationDialog",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(880f, 660f),
                AuthUiTheme.BgDark);

            BuildHeader(panelRt);

            // Верхний блок: Имя, Пол и Пресеты (высота 80)
            var topBar = AuthUiTheme.CreatePanel(panelRt, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(-24f, 76f), AuthUiTheme.CardBg);
            var (nameObj, nameInp) = AuthUiTheme.CreateInputField(topBar, "NameInput", "Имя персонажа...", "Ратибор", new Vector2(180f, 32f));
            nameObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-280f, 4f);
            _nameInput = nameInp;
            _nameInput.characterLimit = 16;
            _nameInput.onValueChanged.AddListener(_ => ValidateForm());

            var (genderObj, _) = AuthUiTheme.CreateButton(topBar, "GenderBtn", "Пол: Мужской ♂", new Vector2(130f, 32f), AuthUiTheme.SecondaryBtn, AuthUiTheme.TextPrimary, ToggleGender);
            genderObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-115f, 4f);
            _genderBtnText = genderObj.GetComponentInChildren<TextMeshProUGUI>();

            BuildPresetsRow(topBar);

            // Левый блок: Характеристики (STR, DEX, INT)
            var statsRt = AuthUiTheme.CreatePanel(panelRt, "StatsBlock", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -164f), new Vector2(290f, 420f), AuthUiTheme.CardBg);
            var statsHeader = AuthUiTheme.CreateText(statsRt, "StatsHeader", "ХАРАКТЕРИСТИКИ (80 ОЧКОВ)", 12f, FontStyles.Bold, AuthUiTheme.GoldAccent, TextAlignmentOptions.Center);
            statsHeader.GetComponent<RectTransform>().offsetMin = new Vector2(0f, 385f);

            _statsCounterText = AuthUiTheme.CreateText(statsRt, "StatsCounter", "", 11f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);
            _statsCounterText.GetComponent<RectTransform>().offsetMin = new Vector2(0f, 360f);

            _strValText = BuildStatRow(statsRt, "СИЛА (STR):", 300f, delta => ModifyStat(ref _str, delta));
            _dexValText = BuildStatRow(statsRt, "ЛОВКОСТЬ (DEX):", 230f, delta => ModifyStat(ref _dex, delta));
            _intValText = BuildStatRow(statsRt, "ИНТЕЛЛЕКТ (INT):", 160f, delta => ModifyStat(ref _int, delta));

            var statsHint = AuthUiTheme.CreateText(statsRt, "StatsHint", "Сила влияет на Здоровье и урон в ближнем бою.\nЛовкость влияет на скорость атаки и Выносливость.\nИнтеллект влияет на запас Маны и силу чар.", 10f, FontStyles.Italic, AuthUiTheme.TextMuted, TextAlignmentOptions.TopLeft);
            statsHint.GetComponent<RectTransform>().offsetMin = new Vector2(14f, 10f);
            statsHint.GetComponent<RectTransform>().offsetMax = new Vector2(-14f, 130f);

            // Правый блок: Навыки (Вариант Б)
            var skillsRt = AuthUiTheme.CreatePanel(panelRt, "SkillsBlock", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(310f, -164f), new Vector2(-322f, 420f), Color.clear);
            _skillsPanel = new CharacterCreationSkillsPanel(skillsRt);
            _skillsPanel.OnSkillsChanged += ValidateForm;

            // Подвал: Кнопки Назад, Статус и Создать
            var footerRt = AuthUiTheme.CreatePanel(panelRt, "Footer", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 64f), AuthUiTheme.PanelHeader);

            var (backObj, _) = AuthUiTheme.CreateButton(footerRt, "BackBtn", "← Назад", new Vector2(140f, 38f), AuthUiTheme.SecondaryBtn, AuthUiTheme.TextPrimary, () => OnBackToSelectRequested?.Invoke());
            backObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-340f, 0f);

            _validationText = AuthUiTheme.CreateText(footerRt, "Validation", "", 11f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);
            var valRt = _validationText.GetComponent<RectTransform>();
            valRt.anchoredPosition = new Vector2(-30f, 0f);
            valRt.sizeDelta = new Vector2(440f, 40f);

            var (submitObj, submitBtn) = AuthUiTheme.CreateButton(footerRt, "SubmitBtn", "СОЗДАТЬ И ВОЙТИ", new Vector2(210f, 42f), AuthUiTheme.PrimaryBtn, Color.white, HandleSubmit);
            submitObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(310f, 0f);
            _submitBtn = submitBtn;

            // По умолчанию выбираем пресет Кузнец-Шахтер
            ApplyPreset(CharacterPresetType.MinerBlacksmith);
        }

        private static void BuildHeader(Transform parent)
        {
            var headerRt = AuthUiTheme.CreatePanel(parent, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 74f), AuthUiTheme.PanelHeader);
            var title = AuthUiTheme.CreateText(headerRt, "Title", "СОЗДАНИЕ ПЕРСОНАЖА", 20f, FontStyles.Bold, AuthUiTheme.GoldAccent, TextAlignmentOptions.Center);
            title.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 12f);
            var subtitle = AuthUiTheme.CreateText(headerRt, "Subtitle", "Настройте характеристики, выберите 3 начальных навыка и получите стартовую экипировку", 11f, FontStyles.Normal, AuthUiTheme.TextMuted, TextAlignmentOptions.Center);
            subtitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -12f);
        }

        private void BuildPresetsRow(Transform parent)
        {
            (string label, CharacterPresetType preset)[] presets =
            {
                ("Воин", CharacterPresetType.Warrior),
                ("Маг", CharacterPresetType.Mage),
                ("Шахтёр-Кузнец", CharacterPresetType.MinerBlacksmith),
                ("Плотник", CharacterPresetType.LumberjackCarpenter),
                ("Лучник", CharacterPresetType.Archer),
                ("Алхимик", CharacterPresetType.Alchemist)
            };

            for (int i = 0; i < presets.Length; i++)
            {
                var p = presets[i];
                float x = 20f + i * 82f;
                var (btnObj, _) = AuthUiTheme.CreateButton(parent, $"Preset_{p.preset}", p.label, new Vector2(78f, 32f), AuthUiTheme.SecondaryBtn, AuthUiTheme.GoldAccent, () => ApplyPreset(p.preset));
                btnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(x - 160f, 4f);
            }
        }

        private TextMeshProUGUI BuildStatRow(Transform parent, string label, float yPos, Action<int> onDelta)
        {
            var rowObj = new GameObject($"Row_{label}", typeof(RectTransform));
            rowObj.transform.SetParent(parent, false);
            var rt = rowObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yPos);
            rt.sizeDelta = new Vector2(260f, 50f);

            var lbl = AuthUiTheme.CreateText(rowObj.transform, "Label", label, 11f, FontStyles.Bold, AuthUiTheme.TextPrimary, TextAlignmentOptions.Left);
            lbl.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 26f);

            var (m5, _) = AuthUiTheme.CreateButton(rowObj.transform, "M5", "-5", new Vector2(30f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(-5));
            m5.GetComponent<RectTransform>().anchoredPosition = new Vector2(-80f, -10f);

            var (m1, _) = AuthUiTheme.CreateButton(rowObj.transform, "M1", "-", new Vector2(24f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(-1));
            m1.GetComponent<RectTransform>().anchoredPosition = new Vector2(-46f, -10f);

            var val = AuthUiTheme.CreateText(rowObj.transform, "Val", "0", 14f, FontStyles.Bold, AuthUiTheme.GoldAccent, TextAlignmentOptions.Center);
            val.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -10f);
            val.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 24f);

            var (p1, _) = AuthUiTheme.CreateButton(rowObj.transform, "P1", "+", new Vector2(24f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(1));
            p1.GetComponent<RectTransform>().anchoredPosition = new Vector2(46f, -10f);

            var (p5, _) = AuthUiTheme.CreateButton(rowObj.transform, "P5", "+5", new Vector2(30f, 24f), AuthUiTheme.SecondaryBtn, Color.white, () => onDelta(5));
            p5.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, -10f);

            return val;
        }

        private void ToggleGender()
        {
            _isFemale = !_isFemale;
            if (_genderBtnText != null)
            {
                _genderBtnText.text = _isFemale ? "Пол: Женский ♀" : "Пол: Мужской ♂";
            }
        }

        public void PrepareForSlot(int slotIndex)
        {
            _targetSlotIndex = slotIndex;
            ApplyPreset(CharacterPresetType.MinerBlacksmith);
        }

        private void ApplyPreset(CharacterPresetType preset)
        {
            var def = CharacterCreationArgsDto.CreateFromPreset(preset, _nameInput.text, _isFemale);
            _str = def.Strength;
            _dex = def.Dexterity;
            _int = def.Intelligence;
            _skillsPanel.ApplyPreset(def.Skill1, def.Skill1Value, def.Skill2, def.Skill2Value, def.Skill3, def.Skill3Value);
            RefreshStatsUI();
            ValidateForm();
        }

        private void ModifyStat(ref int stat, int delta)
        {
            int currentTotal = _str + _dex + _int;
            int target = Mathf.Clamp(stat + delta, MinStat, MaxStat);
            int change = target - stat;
            if (currentTotal + change > TotalTargetStats) target = stat + (TotalTargetStats - currentTotal);
            stat = target;
            RefreshStatsUI();
            ValidateForm();
        }

        private void RefreshStatsUI()
        {
            _strValText.text = _str.ToString();
            _dexValText.text = _dex.ToString();
            _intValText.text = _int.ToString();
            int total = _str + _dex + _int;
            _statsCounterText.text = $"Очки: {total}/{TotalTargetStats} {(total == TotalTargetStats ? "✓" : "")}";
            _statsCounterText.color = total == TotalTargetStats ? AuthUiTheme.SuccessGreen : AuthUiTheme.GoldAccent;
        }

        private void ValidateForm()
        {
            string name = _nameInput.text.Trim();
            int statsSum = _str + _dex + _int;
            bool statsOk = statsSum == TotalTargetStats;
            bool skillsOk = _skillsPanel.IsValid;
            bool nameOk = !string.IsNullOrEmpty(name) && name.Length >= 2 && System.Text.Encoding.UTF8.GetByteCount(name) <= 29;

            if (!nameOk)
            {
                _validationText.text = "Введите имя персонажа (от 2 до 16 символов)";
                _validationText.color = AuthUiTheme.ErrorRed;
                _submitBtn.interactable = false;
            }
            else if (!statsOk)
            {
                _validationText.text = $"Распределите характеристики: {statsSum}/{TotalTargetStats}";
                _validationText.color = AuthUiTheme.ErrorRed;
                _submitBtn.interactable = false;
            }
            else if (!skillsOk)
            {
                _validationText.text = $"Выберите 3 навыка и распределите 100 очков ({_skillsPanel.TotalPointsAllocated}/100)";
                _validationText.color = AuthUiTheme.ErrorRed;
                _submitBtn.interactable = false;
            }
            else
            {
                _validationText.text = "✓ Все параметры верны! Готов к созданию";
                _validationText.color = AuthUiTheme.SuccessGreen;
                _submitBtn.interactable = true;
            }
        }

        private void HandleSubmit()
        {
            string name = _nameInput.text.Trim();
            var skills = _skillsPanel.SelectedSkills;
            var args = new CharacterCreationArgsDto(
                name, _isFemale,
                (byte)_str, (byte)_dex, (byte)_int,
                skills[0].skill, (byte)skills[0].value,
                skills[1].skill, (byte)skills[1].value,
                skills[2].skill, (byte)skills[2].value);

            _submitBtn.interactable = false;
            _validationText.text = "Создание персонажа на сервере...";
            OnCreateCharacterSubmitted?.Invoke(args, _targetSlotIndex);
        }

        public void Show() => RootObject.SetActive(true);
        public void Hide() => RootObject.SetActive(false);
    }
}
