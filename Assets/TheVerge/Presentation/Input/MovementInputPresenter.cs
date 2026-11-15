using System;
using TheVerge.Core.Movement;
using TheVerge.Presentation.UI.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TheVerge.Presentation.Input
{
    /// <summary>
    /// Клавиши движения (PLAN Стадия 3, пп. 1, 9; CONTROLS §4) → <see cref="MoveIntent"/>. W/A/S/D — по положению на
    /// клавиатуре (клавиши Input System физические: на русской раскладке те же), `~` (Backquote) — «всегда бег» ↔
    /// «всегда шаг», Shift — инвертирует на время удержания. Окно без фокуса или курсор в текстовом поле — ввод
    /// заблокирован, клавиши считаются отпущенными. Раскладка «клавиши → направление» — по камере. Индикатор «Бег / Шаг»
    /// обновляется, только когда режим сменился. Вызывается раз в кадр до тика ядра: нажатие и шаг — в одном кадре.
    /// </summary>
    public sealed class MovementInputPresenter : IDisposable
    {
        private readonly MoveIntent _intent;
        private readonly RunIndicatorView _indicator;
        private readonly Func<bool> _textInputFocused;
        private bool _blocked;
        private bool _modeShown;
        private bool _shownRunning;

        /// <param name="textInputFocused">Курсор в видимом поле ввода UI Toolkit.</param>
        public MovementInputPresenter(MoveIntent intent, RunIndicatorView indicator, Func<bool> textInputFocused)
        {
            _intent = intent;
            _indicator = indicator;
            _textInputFocused = textInputFocused;
        }

        public void Update(CameraLayout layout)
        {
            _intent.Layout = layout;
            var keyboard = Keyboard.current;
            SetBlocked(keyboard == null || !Application.isFocused || _textInputFocused());
            if (!_blocked)
            {
                ReadKeys(keyboard);
            }

            ShowMode();
        }

        public void Dispose() => _indicator.Hide();

        private void ReadKeys(Keyboard keyboard)
        {
            Key(keyboard.wKey, MoveKeys.Up);
            Key(keyboard.dKey, MoveKeys.Right);
            Key(keyboard.sKey, MoveKeys.Down);
            Key(keyboard.aKey, MoveKeys.Left);
            _intent.SetShift(keyboard.shiftKey.isPressed);
            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                _intent.ToggleRun();
            }
        }

        // Нажатие и отпускание в одном кадре (очень короткий тап) — оба: MoveIntent запоминает нажатие и даёт шаг.
        private void Key(KeyControl key, MoveKeys move)
        {
            if (key.wasPressedThisFrame)
            {
                _intent.Press(move);
            }

            if (key.wasReleasedThisFrame)
            {
                _intent.Release(move);
            }
        }

        private void SetBlocked(bool blocked)
        {
            if (blocked != _blocked)
            {
                _blocked = blocked;
                _intent.SetBlocked(blocked);
            }
        }

        private void ShowMode()
        {
            bool running = _intent.IsRunning;
            if (_modeShown && running == _shownRunning)
            {
                return;
            }

            _modeShown = true;
            _shownRunning = running;
            _indicator.ShowMode(running);
        }
    }
}
