using System;
using System.Collections.Generic;
using TheVerge.Core.Session;
using TheVerge.Core.Session.Screens;
using UnityEngine.UIElements;

namespace TheVerge.Presentation.UI.Session
{
    /// <summary>
    /// Четыре строки навыков окна создания: выбор навыка из разрешённых сервером (<see cref="CharacterForm.SkillOptions"/>)
    /// и значение. Строки в разметке — skill0…skill3 и skillValue0…skillValue3.
    /// </summary>
    public sealed class CreateSkillRows : IDisposable
    {
        private readonly CharacterForm _form;
        private readonly List<string> _choices;
        private readonly DropdownField[] _skills = new DropdownField[ServerLimits.CreationSkillCount];
        private readonly IntegerField[] _values = new IntegerField[ServerLimits.CreationSkillCount];
        private readonly EventCallback<ChangeEvent<string>>[] _onSkill = new EventCallback<ChangeEvent<string>>[ServerLimits.CreationSkillCount];
        private readonly EventCallback<ChangeEvent<int>>[] _onValue = new EventCallback<ChangeEvent<int>>[ServerLimits.CreationSkillCount];

        public CreateSkillRows(VisualElement panel, CharacterForm form, List<string> choices)
        {
            _form = form;
            _choices = choices;
            for (var row = 0; row < _skills.Length; row++)
            {
                var r = row;
                _skills[row] = UiElements.Require<DropdownField>(panel, "skill" + row);
                _values[row] = UiElements.Require<IntegerField>(panel, "skillValue" + row);
                _skills[row].choices = choices;
                _onSkill[row] = _ => OnSkill(r);
                _onValue[row] = e => _form.SetSkillValue(r, e.newValue);
                _skills[row].RegisterValueChangedCallback(_onSkill[row]);
                _values[row].RegisterValueChangedCallback(_onValue[row]);
            }
        }

        /// <summary>Значения строк — из формы.</summary>
        public void Load()
        {
            for (var row = 0; row < _skills.Length; row++)
            {
                var option = IndexOf(_form.SkillOption(row));
                _skills[row].SetValueWithoutNotify(option >= 0 ? _choices[option] : string.Empty);
                _values[row].SetValueWithoutNotify(_form.SkillValue(row));
            }
        }

        public void Dispose()
        {
            for (var row = 0; row < _skills.Length; row++)
            {
                _skills[row].UnregisterValueChangedCallback(_onSkill[row]);
                _values[row].UnregisterValueChangedCallback(_onValue[row]);
            }
        }

        private void OnSkill(int row)
        {
            var option = _skills[row].index;
            if (option >= 0 && option < CharacterForm.SkillOptions.Count)
            {
                _form.SetSkill(row, CharacterForm.SkillOptions[option]);
            }
        }

        private static int IndexOf(int skill)
        {
            var options = CharacterForm.SkillOptions;
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i] == skill)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
