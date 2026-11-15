using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Темп шагов своего персонажа (PLAN Стадия 3, пп. 5, 7): когда можно, спрашивает <see cref="MoveIntent"/> о шаге и
    /// делает его через <see cref="PlayerMotion"/>. Сервер ведёт расписание накопительно — каждый шаг сдвигает время
    /// следующего на задержку (Projects/Server/Mobiles/Mobile.cs, Move: _nextMovementTime += cost), поворот бесплатный и ставит
    /// расписание на «сейчас» (cost == 0). Клиент строит своё так же: опоздание из-за тика до
    /// <see cref="ClientLimits.MoveCatchUpMs"/> догоняется, большее (игрок стоял) не копится, поэтому темп не отстаёт от
    /// сервера и не опережает его; кредит сервера на джиттер клиент не тратит. Поворот и шаг в ту же сторону уходят в одном
    /// тике (CONTROLS §4), но не раньше срока шага; если поворот занял последнее место в очереди, шаг уходит, как только
    /// место освободится. Время — только из <see cref="Update"/> (часы клиента, IClock).
    /// </summary>
    public sealed class StepPacer : ITickHandler
    {
        private readonly MoveIntent _intent;
        private readonly PlayerMotion _motion;
        private bool _hasSchedule;
        private long _nextStepMs;
        private bool _hasFollowUp;
        private Direction _followUpDirection;
        private bool _followUpRun;

        public StepPacer(MoveIntent intent, PlayerMotion motion)
        {
            _intent = intent;
            _motion = motion;
        }

        public void Update(long now)
        {
            if (_hasFollowUp)
            {
                StepAfterTurn(now);
                return;
            }

            if ((_hasSchedule && now < _nextStepMs) || !_motion.CanStep || !_intent.TryTakeStep(out var direction, out bool run))
            {
                return;
            }

            var result = _motion.Step(direction, run);
            if (result == StepResult.Turned)
            {
                // Сервер после поворота считает расписание от «сейчас» — клиент тоже.
                _hasSchedule = false;
                _followUpDirection = direction;
                _followUpRun = run;
                _hasFollowUp = true;
                StepAfterTurn(now);
            }
            else if (result == StepResult.Moved)
            {
                Schedule(now);
            }
        }

        // Шаг в сторону, куда только что повернулись: сразу или, если очередь полна, как только освободится место.
        private void StepAfterTurn(long now)
        {
            var result = _motion.Step(_followUpDirection, _followUpRun);
            if (result == StepResult.QueueFull)
            {
                return;
            }

            _hasFollowUp = false;
            if (result == StepResult.Moved)
            {
                Schedule(now);
            }
        }

        private void Schedule(long now)
        {
            long baseline = _hasSchedule && now - _nextStepMs <= ClientLimits.MoveCatchUpMs ? _nextStepMs : now;
            _nextStepMs = baseline + StepTiming.StepDelayMs(_motion.IsMounted, _motion.IsRunning);
            _hasSchedule = true;
        }
    }
}
