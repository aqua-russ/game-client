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
    /// место освободится, — если до того сервер не переставил персонажа и ввод не заблокирован (тогда шаг относился бы
    /// к прежнему месту и шёл бы мимо воли игрока). Время — только из <see cref="Update"/> (часы клиента, IClock).
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
        private int _followUpResets;
        private int _respondedStarts;

        public StepPacer(MoveIntent intent, PlayerMotion motion)
        {
            _intent = intent;
            _motion = motion;
        }

        /// <summary>Сколько нажатий из покоя получили первый запрос серверу (поворот или шаг).</summary>
        public int ResponseCount { get; private set; }

        /// <summary>
        /// Отклик на последнее нажатие из покоя, мс: от нажатия (<see cref="MoveIntent.LastStartMs"/>) до первого запроса
        /// серверу — ответа сервера клиент не ждёт (шаг предсказан). -1 — отклика ещё не было.
        /// </summary>
        public int LastResponseMs { get; private set; } = -1;

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
            if (result == StepResult.Turned || result == StepResult.Moved)
            {
                NoteResponse(now);
            }

            if (result == StepResult.Turned)
            {
                // Сервер после поворота считает расписание от «сейчас» — клиент тоже.
                _hasSchedule = false;
                _followUpDirection = direction;
                _followUpRun = run;
                _followUpResets = _motion.ServerResets;
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
            if (_motion.ServerResets != _followUpResets || _intent.IsBlocked)
            {
                _hasFollowUp = false;
                return;
            }

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

        // Первый запрос после нажатия из покоя — отклик; дальнейшие шаги той же зажатой клавиши — уже не отклик.
        private void NoteResponse(long now)
        {
            if (_intent.Starts == _respondedStarts)
            {
                return;
            }

            _respondedStarts = _intent.Starts;
            LastResponseMs = (int)(now - _intent.LastStartMs);
            ResponseCount++;
        }

        private void Schedule(long now)
        {
            long baseline = _hasSchedule && now - _nextStepMs <= ClientLimits.MoveCatchUpMs ? _nextStepMs : now;
            _nextStepMs = baseline + StepTiming.StepDelayMs(_motion.IsMounted, _motion.IsRunning);
            _hasSchedule = true;
        }
    }
}
