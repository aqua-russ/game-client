using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Движение своего персонажа (PLAN Стадия 3, пп. 4–6; ADR 0020 пп. 1–3): клетка и направление по предсказанию клиента,
    /// бег, верховая езда и шаги, ждущие ответа сервера. Правила — Projects/Server/Mobiles/Mobile.cs: шаг в новую сторону
    /// только поворачивает (Move), шаг в ту же сторону проверяется физикой и правилом прохода сквозь мобов
    /// (<see cref="StepPredictor"/>); уставший пешком идёт шагом (<see cref="RunFatigue"/>). Ответы сервера разбирает <see cref="MovementReplies"/>.
    /// В мир клиента (<see cref="EntityStore"/>) идёт только клетка, которую подтвердил сервер: по ней удаляются далёкие
    /// объекты, а после отката предсказанного шага сервер их заново не пришлёт.
    /// </summary>
    public sealed class PlayerMotion
    {
        private readonly EntityStore _world;
        private readonly StepPredictor _predictor;
        private readonly IMovementSender _sender;
        private readonly MovementSequenceQueue _queue = new MovementSequenceQueue();
        private bool _placed;

        public PlayerMotion(EntityStore world, MovementCheck check, IMovementSender sender)
        {
            _world = world;
            _predictor = new StepPredictor(check);
            _sender = sender;
        }

        /// <summary>Клетка, где персонаж стоит по предсказанию (сервер мог ещё не ответить на шаги).</summary>
        public Point3D Position { get; private set; }

        /// <summary>Куда персонаж шагает и смотрит (без бита бега).</summary>
        public Direction StepDirection { get; private set; }

        /// <summary>Куда повёрнуто тело. Пока совпадает с <see cref="StepDirection"/>: своё направление тела — трек F (ADR 0006).</summary>
        public Direction BodyDirection => StepDirection;

        /// <summary>Бег у последнего запроса (или у направления, присланного сервером при откате).</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Верхом: у своего персонажа надет предмет на слое верховой езды (Layer.Mount, 0x19).</summary>
        public bool IsMounted => _world.Player?.GetEquipped(Layer.Mount) != null;

        /// <summary>Шагов и поворотов, ждущих ответа сервера.</summary>
        public int PendingSteps => _queue.PendingCount;

        /// <summary>Клиент попросил о ресинхронизации и ждёт места от сервера (0x20): шагов нет.</summary>
        public bool IsResyncing { get; private set; }

        /// <summary>
        /// Сколько раз сервер сам ставил персонажа или отменял ждущие шаги (вход, отказ, 0x20, смена карты, ресинхронизация):
        /// изменилось — отложенное намерение (шаг после поворота, <see cref="StepPacer"/>) относится к прежнему месту.
        /// </summary>
        public int ServerResets { get; private set; }

        /// <summary>Сколько шагов (не поворотов) ушло серверу с начала — для замера темпа и оверлея.</summary>
        public int MovesSent { get; private set; }

        /// <summary>Шаг или поворот сейчас уйдёт: персонаж в мире, ресинхронизации нет, в очереди есть место.</summary>
        public bool CanStep => _placed && !IsResyncing && _queue.CanRequestStep && _world.Player != null;

        /// <summary>
        /// Шагнуть или повернуться в направлении <paramref name="direction"/> (бит бега в нём не учитывается). Уставший
        /// пешком персонаж идёт шагом, даже если просили бег.
        /// </summary>
        public StepResult Step(Direction direction, bool run)
        {
            var player = _world.Player;
            if (!_placed || player == null)
            {
                return StepResult.NotInWorld;
            }

            if (IsResyncing)
            {
                return StepResult.Resyncing;
            }

            if (!_queue.CanRequestStep)
            {
                return StepResult.QueueFull;
            }

            run = run && (player.IsGhost || IsMounted || RunFatigue.AllowsRun(player.Status.Stam));
            Direction facing = direction & Direction.FacingMask;
            if (facing != StepDirection)
            {
                Send(facing, run, Position);
                StepDirection = facing;
                return StepResult.Turned;
            }

            if (!_predictor.TryPredict(player, Position, facing, out var target, out bool shoves))
            {
                return StepResult.Blocked;
            }

            byte sequence = Send(facing, run, target);
            if (shoves)
            {
                _predictor.OnShoveSent(sequence);
            }

            Position = target;
            MovesSent++;
            return StepResult.Moved;
        }

        /// <summary>
        /// Подтверждение сервера (0x22): снимает самый старый ждущий шаг, и мир клиента узнаёт подтверждённую клетку и
        /// известность своего персонажа. Другой номер — false, ничего не меняется (рассинхрон решает вызывающий).
        /// </summary>
        public bool Confirm(byte sequence, byte notoriety = MobileInfo.NotorietyUnknown)
        {
            if (!_queue.ConfirmStep(sequence, out var step))
            {
                return false;
            }

            _predictor.OnConfirmed(step.Sequence);
            PlaceInWorld(step.TargetPosition, step.Direction, notoriety);
            return true;
        }

        /// <summary>
        /// Отказ сервера (0x21). Настоящий — персонаж встаёт на клетку и направление сервера, остальные ждущие шаги
        /// отменены, следующий шаг с номером 0; отказ в уже отменённом шаге — старый: false, ничего не меняется.
        /// </summary>
        public bool Reject(byte sequence, Point3D position, Direction direction)
        {
            if (!_queue.Reject(sequence))
            {
                return false;
            }

            StandAt(position, direction);
            return true;
        }

        /// <summary>Вход в мир (0x1B): персонаж на клетке сервера, прежних шагов нет, первый шаг с номером 0.</summary>
        public void EnterWorld(Point3D position, Direction direction)
        {
            _queue.Reset();
            IsResyncing = false;
            StandAt(position, direction);
        }

        /// <summary>
        /// Сервер сам поставил персонажа (0x20: телепорт, ресинхронизация, смена карты, тела, цвета): ждущие шаги отменены,
        /// следующий шаг с номером 0. 0x20 во время ресинхронизации — ответ на неё: сервер шлёт его и только потом сбрасывает
        /// счёт (IncomingPlayerPackets.Resynchronize), ответы на все прежние шаги уже пришли, и номера отменённых шагов
        /// забываются — иначе подтверждение нового шага 0 снова считалось бы рассинхроном, и так после каждого шага.
        /// </summary>
        public void ResetToServer(Point3D position, Direction direction)
        {
            if (IsResyncing)
            {
                _queue.Reset();
            }
            else
            {
                _queue.CancelPending();
            }

            IsResyncing = false;
            StandAt(position, direction);
        }

        /// <summary>Другая карта (0xBF 0x08): ждущие шаги отменены; шагать некуда, пока сервер не пришлёт место (0x20).</summary>
        public void OnMapChanged()
        {
            ServerResets++;
            _queue.CancelPending();
            _placed = false;
        }

        /// <summary>Клиент шлёт запрос ресинхронизации: ждущие шаги отменены, шагов нет до места от сервера (0x20).</summary>
        public void BeginResync()
        {
            ServerResets++;
            _queue.CancelPending();
            IsResyncing = true;
        }

        private byte Send(Direction facing, bool run, Point3D target)
        {
            _queue.TryEnqueueStep(facing, target, out byte sequence);
            _sender.SendStep(facing, run, sequence);
            IsRunning = run;
            return sequence;
        }

        private void StandAt(Point3D position, Direction direction)
        {
            ServerResets++;
            _predictor.Forget();
            _placed = true;
            Position = position;
            StepDirection = direction & Direction.FacingMask;
            IsRunning = (direction & Direction.Running) != 0;
            PlaceInWorld(position, StepDirection, MobileInfo.NotorietyUnknown);
        }

        private void PlaceInWorld(Point3D position, Direction facing, byte notoriety)
        {
            var player = _world.Player;
            if (player != null)
            {
                _world.UpdateMobile(player.Serial, new MobileInfo(player.Body, position, facing, player.Hue, player.Flags, notoriety));
            }
        }
    }
}
