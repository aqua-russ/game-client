using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Чего хочет игрок клавишами движения (PLAN Стадия 3, п. 1; CONTROLS §4): направление из W/A/S/D по раскладке камеры
    /// и бег. Бег по умолчанию включён, `~` его переключает, Shift на время удержания инвертирует. Когда шагать, решает
    /// темп шагов (PLAN 3.7): он спрашивает <see cref="TryTakeStep"/>, как только можно сделать следующий шаг.
    /// Пару клавиш не нажимают ровно одновременно, поэтому первый шаг из покоя ждёт вторую клавишу
    /// <see cref="ClientLimits.MovePairWaitMs"/>. Нажатие между шагами не теряется: следующий шаг идёт в направление,
    /// которое было в момент последнего нажатия (так короткий тап даёт шаг), дальше — по зажатым клавишам.
    /// </summary>
    public sealed class MoveIntent
    {
        private readonly IClock _clock;
        private MoveKeys _held;
        private bool _shiftHeld;
        private bool _runToggle = true;
        private bool _blocked;
        private bool _hasRemembered;
        private Direction _remembered;
        private bool _rememberedIsDiagonal;
        private bool _waitingForPair;
        private long _firstPressMs;

        public MoveIntent(IClock clock, CameraLayout layout)
        {
            _clock = clock;
            Layout = layout;
        }

        /// <summary>Раскладка камеры: по ней клавиши превращаются в направление UO (<see cref="MoveDirectionTable"/>).</summary>
        public CameraLayout Layout { get; set; }

        /// <summary>Сколько раз клавишу движения нажали из покоя (ни одна не была зажата) — для замера отклика (<see cref="StepPacer"/>).</summary>
        public int Starts { get; private set; }

        /// <summary>Когда клавишу движения нажали из покоя в последний раз (часы клиента).</summary>
        public long LastStartMs { get; private set; }

        /// <summary>Бег для индикатора «Бег / Шаг» и для следующего шага: переключатель `~`, обращённый удержанием Shift.</summary>
        public bool IsRunning => _runToggle ^ _shiftHeld;

        public void Press(MoveKeys key)
        {
            if (_blocked || (_held & key) == key)
            {
                return;
            }

            if (_held == MoveKeys.None && !_hasRemembered)
            {
                _waitingForPair = true;
                _firstPressMs = _clock.NowMs;
                LastStartMs = _firstPressMs;
                Starts++;
            }

            _held |= key;
            _hasRemembered = MoveDirectionTable.TryGetDirection(_held, Layout, out _remembered);
            _rememberedIsDiagonal = MoveDirectionTable.IsScreenDiagonal(_held);
        }

        public void Release(MoveKeys key) => _held &= ~key;

        public void SetShift(bool held)
        {
            if (!_blocked)
            {
                _shiftHeld = held;
            }
        }

        /// <summary>`~`: «всегда бег» ↔ «всегда шаг».</summary>
        public void ToggleRun()
        {
            if (!_blocked)
            {
                _runToggle = !_runToggle;
            }
        }

        /// <summary>Ввод заблокирован (<see cref="SetBlocked"/>): окно без фокуса или курсор в текстовом поле.</summary>
        public bool IsBlocked => _blocked;

        /// <summary>
        /// Окно без фокуса или курсор в текстовом поле: все клавиши и Shift отпущены, запомненное нажатие забыто, ввод
        /// молчит до снятия блокировки. Переключатель бега остаётся. Клавиши, зажатые до блокировки, после неё не считаются.
        /// </summary>
        public void SetBlocked(bool blocked)
        {
            _blocked = blocked;
            if (blocked)
            {
                _held = MoveKeys.None;
                _shiftHeld = false;
                _hasRemembered = false;
                _waitingForPair = false;
            }
        }

        /// <summary>Следующий шаг, если он есть и ожидание второй клавиши кончилось; запомненное нажатие расходуется.</summary>
        public bool TryTakeStep(out Direction direction, out bool run)
        {
            run = IsRunning;
            bool diagonal;
            if (_hasRemembered)
            {
                direction = _remembered;
                diagonal = _rememberedIsDiagonal;
            }
            else if (!_blocked && MoveDirectionTable.TryGetDirection(_held, Layout, out direction))
            {
                diagonal = MoveDirectionTable.IsScreenDiagonal(_held);
            }
            else
            {
                direction = Direction.North;
                return false;
            }

            if (_waitingForPair && !diagonal && _clock.NowMs - _firstPressMs < ClientLimits.MovePairWaitMs)
            {
                return false;
            }

            _waitingForPair = false;
            _hasRemembered = false;
            return true;
        }
    }
}
