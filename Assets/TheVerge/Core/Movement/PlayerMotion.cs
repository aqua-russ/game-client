using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Движение своего персонажа (PLAN Стадия 3, п. 4; ADR 0020 пп. 1–3): клетка и направление по предсказанию клиента,
    /// бег, верховая езда и шаги, ждущие ответа сервера. Правила — Projects/Server/Mobiles/Mobile.cs: шаг в новую сторону
    /// только поворачивает (Move), шаг в ту же сторону проверяется физикой (CanMove → <see cref="MovementCheck"/>),
    /// сквозь живого моба игрок проходит лишь с полной выносливостью, и сервер сразу снимает 10 (CheckShove).
    /// В мир клиента (<see cref="EntityStore"/>) идёт только клетка, которую подтвердил сервер: по ней удаляются далёкие
    /// объекты, а после отката предсказанного шага сервер их заново не пришлёт.
    /// </summary>
    public sealed class PlayerMotion
    {
        private readonly EntityStore _world;
        private readonly MovementCheck _check;
        private readonly IMovementSender _sender;
        private readonly MovementSequenceQueue _queue = new MovementSequenceQueue();
        private bool _placed;
        private bool _shoveUnanswered;
        private byte _shoveSequence;

        public PlayerMotion(EntityStore world, MovementCheck check, IMovementSender sender)
        {
            _world = world;
            _check = check;
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

        /// <summary>Шагнуть или повернуться в направлении <paramref name="direction"/> (бит бега в нём не учитывается).</summary>
        public StepResult Step(Direction direction, bool run)
        {
            var player = _world.Player;
            if (!_placed || player == null)
            {
                return StepResult.NotInWorld;
            }

            if (!_queue.CanRequestStep)
            {
                return StepResult.QueueFull;
            }

            Direction facing = direction & Direction.FacingMask;
            if (facing != StepDirection)
            {
                Send(facing, run, Position);
                StepDirection = facing;
                return StepResult.Turned;
            }

            if (!TryPredict(player, facing, out var target, out bool shoved))
            {
                return StepResult.Blocked;
            }

            byte sequence = Send(facing, run, target);
            if (shoved)
            {
                _shoveUnanswered = true;
                _shoveSequence = sequence;
            }

            Position = target;
            return StepResult.Moved;
        }

        /// <summary>
        /// Подтверждение сервера (0x22): снимает самый старый ждущий шаг, и мир клиента узнаёт подтверждённую клетку.
        /// Другой номер — false, ничего не меняется (рассинхрон решает вызывающий).
        /// </summary>
        public bool Confirm(byte sequence)
        {
            if (!_queue.ConfirmStep(sequence, out var step))
            {
                return false;
            }

            if (_shoveUnanswered && step.Sequence == _shoveSequence)
            {
                _shoveUnanswered = false;
            }

            PlaceInWorld(step.TargetPosition, step.Direction);
            return true;
        }

        /// <summary>
        /// Сервер сам поставил персонажа: вход в мир или отказ шага 0x21 (откат). Персонаж встаёт на клетку и направление
        /// сервера, очередь пуста, следующий шаг снова с номером 0.
        /// </summary>
        public void ResetToServer(Point3D position, Direction direction)
        {
            _queue.Reset();
            _shoveUnanswered = false;
            _placed = true;
            Position = position;
            StepDirection = direction & Direction.FacingMask;
            IsRunning = (direction & Direction.Running) != 0;
            PlaceInWorld(position, StepDirection);
        }

        // Шаг без прохода сквозь мобов; не вышло — с проходом, если он разрешён (shoved: шаг стоил выносливости).
        // Призраку проход и так разрешён и бесплатен (MoverState.FromMobile, CheckShove: !Alive).
        private bool TryPredict(Mobile player, Direction facing, out Point3D target, out bool shoved)
        {
            var mover = MoverState.FromMobile(player);
            shoved = false;
            if (!_check.CheckMovement(Position, facing, mover, out int newZ))
            {
                shoved = !mover.CanShoveMobiles && CanShove(player)
                    && _check.CheckMovement(Position, facing, MoverState.FromMobile(player, canShoveMobiles: true), out newZ);
                if (!shoved)
                {
                    target = Position;
                    return false;
                }
            }

            var cell = Position.GetStepPoint(facing);
            target = new Point3D(cell.X, cell.Y, (sbyte)newZ);
            return true;
        }

        // Mobile.CheckShove: проход стоит 10 выносливости и разрешён только при полной. Пока сервер не ответил на шаг
        // сквозь моба, выносливость в статусе ещё старая, а сервер её уже снял — второй раз подряд не проходим.
        private bool CanShove(Mobile player)
        {
            var stamina = player.Status.Stam;
            return !_shoveUnanswered && stamina.Maximum > 0 && stamina.Current == stamina.Maximum;
        }

        private byte Send(Direction facing, bool run, Point3D target)
        {
            _queue.TryEnqueueStep(facing, target, out byte sequence);
            _sender.SendStep(facing, run, sequence);
            IsRunning = run;
            return sequence;
        }

        private void PlaceInWorld(Point3D position, Direction facing)
        {
            var player = _world.Player;
            if (player != null)
            {
                _world.UpdateMobile(player.Serial, new MobileInfo(player.Body, position, facing, player.Hue, player.Flags, MobileInfo.NotorietyUnknown));
            }
        }
    }
}
