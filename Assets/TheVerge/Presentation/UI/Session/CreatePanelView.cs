using System;
using System.Collections.Generic;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>
    /// Окно создания персонажа: имя, пол, три стата, четыре навыка, город. Всё сразу уходит в <see cref="CharacterForm"/>;
    /// подсказки под частями и активность «Создать» — оттуда же (правила сервера только в ядре).
    /// </summary>
    public sealed class CreatePanelView : IDisposable
    {
        private static readonly List<string> SkillChoices = SkillChoiceNames();
        private readonly LoginScreens _screens;
        private readonly CharacterForm _form;
        private readonly CreateSkillRows _skills;
        private readonly TextField _name;
        private readonly Toggle _female;
        private readonly IntegerField[] _stats = new IntegerField[3];
        private readonly DropdownField _city;
        private readonly Label[] _labels;
        private readonly Button _create;
        private readonly Button _cancel;

        public CreatePanelView(VisualElement root, LoginScreens screens)
        {
            _screens = screens;
            _form = screens.Creation;
            Panel = UiElements.Require<VisualElement>(root, "createPanel");
            _name = UiElements.Require<TextField>(Panel, "nameField");
            _female = UiElements.Require<Toggle>(Panel, "femaleToggle");
            _stats[0] = UiElements.Require<IntegerField>(Panel, "strengthField");
            _stats[1] = UiElements.Require<IntegerField>(Panel, "dexterityField");
            _stats[2] = UiElements.Require<IntegerField>(Panel, "intelligenceField");
            _city = UiElements.Require<DropdownField>(Panel, "cityField");
            _labels = Array.ConvertAll(
                new[] { "nameHint", "statsTotal", "statsHint", "skillsTotal", "skillsHint", "cityHint" },
                name => UiElements.Require<Label>(Panel, name));
            _create = UiElements.Require<Button>(Panel, "createConfirm");
            _cancel = UiElements.Require<Button>(Panel, "createCancel");
            _skills = new CreateSkillRows(Panel, _form, SkillChoices);
            Subscribe(true);
        }

        public VisualElement Panel { get; }

        /// <summary>Окно только что открыто: поля — из свежей формы (сброшенной ядром), города — из списка сервера.</summary>
        public void Load()
        {
            _name.SetValueWithoutNotify(_form.Name);
            _female.SetValueWithoutNotify(_form.Female);
            _stats[0].SetValueWithoutNotify(_form.Strength);
            _stats[1].SetValueWithoutNotify(_form.Dexterity);
            _stats[2].SetValueWithoutNotify(_form.Intelligence);
            _skills.Load();
            var cities = new List<string>();
            foreach (var city in _form.Cities)
            {
                cities.Add(city.ToString());
            }

            _city.choices = cities;
            _city.SetValueWithoutNotify(_form.City >= 0 && _form.City < cities.Count ? cities[_form.City] : string.Empty);
        }

        public void Refresh()
        {
            UiElements.SetText(_labels[0], _form.NameHint);
            UiElements.SetText(_labels[1], _form.StatsTotal);
            UiElements.SetText(_labels[2], _form.StatsHint);
            UiElements.SetText(_labels[3], _form.SkillsTotal);
            UiElements.SetText(_labels[4], _form.SkillsHint);
            UiElements.SetText(_labels[5], _form.CityHint);
            _create.SetEnabled(_form.CanCreate);
        }

        public void Dispose()
        {
            Subscribe(false);
            _skills.Dispose();
        }

        private static List<string> SkillChoiceNames()
        {
            var names = new List<string>();
            foreach (var skill in CharacterForm.SkillOptions)
            {
                names.Add(SkillNames.Name(skill));
            }

            return names;
        }

        private void Subscribe(bool on)
        {
            if (on)
            {
                _name.RegisterValueChangedCallback(OnName);
                _female.RegisterValueChangedCallback(OnFemale);
                _stats[0].RegisterValueChangedCallback(OnStrength);
                _stats[1].RegisterValueChangedCallback(OnDexterity);
                _stats[2].RegisterValueChangedCallback(OnIntelligence);
                _city.RegisterValueChangedCallback(OnCity);
                _create.clicked += _screens.Create;
                _cancel.clicked += _screens.CloseCreation;
                return;
            }

            _name.UnregisterValueChangedCallback(OnName);
            _female.UnregisterValueChangedCallback(OnFemale);
            _stats[0].UnregisterValueChangedCallback(OnStrength);
            _stats[1].UnregisterValueChangedCallback(OnDexterity);
            _stats[2].UnregisterValueChangedCallback(OnIntelligence);
            _city.UnregisterValueChangedCallback(OnCity);
            _create.clicked -= _screens.Create;
            _cancel.clicked -= _screens.CloseCreation;
        }

        private void OnName(ChangeEvent<string> e) => _form.Name = e.newValue;

        private void OnFemale(ChangeEvent<bool> e) => _form.Female = e.newValue;

        private void OnStrength(ChangeEvent<int> e) => _form.Strength = e.newValue;

        private void OnDexterity(ChangeEvent<int> e) => _form.Dexterity = e.newValue;

        private void OnIntelligence(ChangeEvent<int> e) => _form.Intelligence = e.newValue;

        private void OnCity(ChangeEvent<string> e) => _form.City = _city.index;
    }
}
