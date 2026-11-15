using System;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Показ шагов своего персонажа (PLAN Стадия 3, п. 9; ADR 0020 пп. 1, 3, 5, 6): клетку предсказывает
    /// <see cref="PlayerMotion"/>, здесь — когда и за сколько картинка едет в неё; Unity только читает
    /// <see cref="Current"/> и сглаживает по <see cref="ShownStep.Progress"/>.
    /// <list type="bullet">
    /// <item>Предсказанный шаг (<see cref="PlayerMotion.Moved"/>) — езда из прежней клетки за время шага (<see cref="StepTiming"/>
    /// по бегу и коню). Шаг начинается в <see cref="Update"/> того же тика; если картинка ещё показывает прежний шаг — сразу
    /// после него. Темп шагов держит <see cref="StepPacer"/> по расписанию сервера, поэтому картинка отстаёт не больше чем
    /// на шаг; отстала на несколько — догоняет, как у чужих.</item>
    /// <item>Сервер сам поставил персонажа (<see cref="PlayerMotion.Placed"/>: вход в мир, откат по 0x21, место по 0x20) —
    /// перескок на его клетку без езды (событие <see cref="Snapped"/>), ждущие шаги выброшены.</item>
    /// </list>
    /// Время прихода шага — <see cref="IClock"/>; на шаг новой памяти нет. Тикает после <see cref="StepPacer"/>.
    /// </summary>
    public sealed class PlayerStepShow : ITickHandler, IDisposable
    {
        private readonly PlayerMotion _motion;
        private readonly IClock _clock;
        private readonly ShownSteps _steps = new ShownSteps();
        private bool _disposed;

        public PlayerStepShow(PlayerMotion motion, IClock clock)
        {
            _motion = motion;
            _clock = clock;
            if (motion.IsPlaced)
            {
                IsPlaced = true;
                _steps.SnapTo(default, motion.Position, motion.StepDirection, clock.NowMs);
            }

            _motion.Moved += OnMoved;
            _motion.Placed += OnPlaced;
        }

        /// <summary>Картинка встала на клетку без езды (вход в мир, откат, место от сервера).</summary>
        public event Action<MotionSnap>? Snapped;

        /// <summary>Сервер уже поставил персонажа хоть раз: есть что показывать.</summary>
        public bool IsPlaced { get; private set; }

        /// <summary>Текущий шаг картинки; персонаж стоит — шаг на месте (откуда = куда, длительность 0).</summary>
        public ShownStep Current => _steps.Current;

        /// <summary>Сколько предсказанных шагов ждут показа (картинка ещё едет прежний).</summary>
        public int WaitingSteps => _steps.WaitingCount;

        /// <summary>Тик: начать шаги, чья очередь пришла.</summary>
        public void Update(long now)
        {
            while (_steps.TryStartNext(now, out _))
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _motion.Moved -= OnMoved;
            _motion.Placed -= OnPlaced;
        }

        private void OnMoved(StepMove move)
        {
            if (!IsPlaced)
            {
                return;
            }

            if (!_steps.Enqueue(move.To, move.Facing, _clock.NowMs, move.IsMounted))
            {
                Snap(move.To, move.Facing, SnapReason.Backlog);
            }
        }

        private void OnPlaced(Point3D cell, Direction facing, SnapReason reason)
        {
            IsPlaced = true;
            Snap(cell, facing, reason);
        }

        private void Snap(Point3D cell, Direction facing, SnapReason reason)
        {
            long now = _clock.NowMs;
            int dropped = _steps.SnapTo(default, cell, facing, now);
            Snapped?.Invoke(new MotionSnap(cell, reason, dropped, now));
        }
    }
}
