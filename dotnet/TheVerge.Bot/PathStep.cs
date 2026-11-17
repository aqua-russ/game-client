using System;
using System.Collections.Generic;
using TheVerge.Core.Movement;
using TheVerge.Core.Movement.Tooling;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// path:x,y[,z],run|walk (PLAN Стадия 3, п. 11): проход к цели по пути A* поверх <see cref="MovementCheck"/>.
    /// Шагает темпом игрока через <see cref="MoveIntent"/> и <see cref="StepPacer"/>; при появлении препятствия на пути
    /// или откате сервера перестраивает маршрут, а при закрытом пути завершается понятной ошибкой.
    /// </summary>
    internal sealed class PathStep : IBotStep
    {
        private const int StuckMs = 3000;
        private const int SlackMs = 10000;
        private const int MaxReplans = 16;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private readonly PathRequest _request;
        private readonly PathGoal _goal;
        private readonly PaceTracker _pace;
        private readonly List<PathMove> _path = new List<PathMove>(128);
        private PathFinder? _finder;
        private MoveKeys _held;
        private bool _started;
        private int _cursor;
        private int _replans;
        private int _lastResets;
        private int _sentBefore;
        private int _rejectsBefore;
        private int _resyncsBefore;
        private int _lastSent;
        private long _startMs;
        private long _lastSentAt;

        public PathStep(BotWorld world, ILog log, IClock clock, string argument)
        {
            _world = world;
            _log = log;
            _clock = clock;
            _pace = new PaceTracker(clock);
            PathRequest.TryParse(argument, out _request);
            _goal = _request.ToGoal();
        }

        public int TimeoutMs { get; private set; } = SlackMs;

        public string TimeoutProblem =>
            "персонаж не прошёл по пути к " + GoalText() + " за " + TimeoutMs / 1000 + " с, он в " +
            (_world.Entities.Player?.Position.ToString() ?? "неизвестно где") + ".";

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            var movement = _world.Movement;
            var player = _world.Entities.Player;
            if (movement == null || movement.Check == null)
            {
                Failure = "нужны файлы данных (--data): без карты путь не построить.";
                return false;
            }

            if (player == null || !movement.Motion.IsPlaced)
            {
                return false;
            }

            if (!_started)
            {
                Start(movement, player);
                if (Failure.Length > 0)
                {
                    return false;
                }
            }

            return StepAlongPath(movement, player);
        }

        public void Finish()
        {
            _pace.Stop();
            Hold(_world.Movement?.Intent, MoveKeys.None);
        }

        private bool StepAlongPath(BotMovement movement, Mobile player)
        {
            var motion = movement.Motion;
            if (_goal.Matches(motion.Position))
            {
                return FinishArrival(movement, player);
            }

            if (motion.ServerResets != _lastResets)
            {
                _lastResets = motion.ServerResets;
                return HandleInterruption(movement, player);
            }

            AdvanceCursor(motion.Position);
            if (_cursor >= _path.Count || motion.Position != _path[_cursor].From || !IsNextClear(movement.Check!, player, motion.Position))
            {
                return HandleInterruption(movement, player);
            }

            Hold(movement.Intent, CompassDirection.KeysFor(_path[_cursor].Direction));
            CheckStuck(motion);
            return false;
        }

        private bool FinishArrival(BotMovement movement, Mobile player)
        {
            var motion = movement.Motion;
            Hold(movement.Intent, MoveKeys.None);
            if (motion.PendingSteps > 0 || !_goal.Matches(player.Position))
            {
                return false;
            }

            _pace.Stop();
            _log.Info(Report(movement));
            if (_pace.HasTempo)
            {
                _log.Info(_pace.Describe());
            }

            return true;
        }

        private bool HandleInterruption(BotMovement movement, Mobile player)
        {
            Hold(movement.Intent, MoveKeys.None);
            if (movement.Motion.PendingSteps > 0 || movement.Motion.IsResyncing)
            {
                return false;
            }

            Replan(movement, player);
            return false;
        }

        private void Start(BotMovement movement, Mobile player)
        {
            _finder = new PathFinder(movement.Check!);
            _pace.Start(movement.Motion);
            if (movement.Intent.IsRunning != _request.Run)
            {
                movement.Intent.ToggleRun();
            }

            _startMs = _clock.NowMs;
            _lastSentAt = _startMs;
            _sentBefore = _lastSent = movement.Motion.MovesSent;
            _rejectsBefore = movement.Replies.RejectCount;
            _resyncsBefore = movement.Replies.ResyncCount;
            _lastResets = movement.Motion.ServerResets;
            _started = true;
            PlanFrom(movement.Check!, player, movement.Motion.Position);
        }

        private void Replan(BotMovement movement, Mobile player)
        {
            _replans++;
            if (_replans > MaxReplans)
            {
                Failure = "путь постоянно перекрывается (больше " + MaxReplans + " перестроений) у " +
                          movement.Motion.Position + " к " + GoalText() + ".";
                return;
            }

            _lastSentAt = _clock.NowMs;
            PlanFrom(movement.Check!, player, movement.Motion.Position);
        }

        private void PlanFrom(MovementCheck check, Mobile player, Point3D from)
        {
            var mover = MoverState.FromMobile(player, canShoveMobiles: false);
            var result = _finder!.FindPath(from, _goal, mover, _path);
            _cursor = 0;
            if (!result.IsSuccess)
            {
                Failure = DescribePlanFailure(result, from);
                return;
            }

            int elapsed = (int)(_clock.NowMs - _startMs);
            TimeoutMs = Math.Max(TimeoutMs, elapsed + _path.Count * StepTiming.StepDelayMs(false, false) + SlackMs);
        }

        private void AdvanceCursor(Point3D position)
        {
            while (_cursor < _path.Count && position == _path[_cursor].To)
            {
                _cursor++;
            }
        }

        private bool IsNextClear(MovementCheck check, Mobile player, Point3D position)
        {
            var mover = MoverState.FromMobile(player, canShoveMobiles: false);
            return check.CheckMovement(position, _path[_cursor].Direction, mover, out _);
        }

        private void Hold(MoveIntent? intent, MoveKeys keys)
        {
            if (intent == null || keys == _held)
            {
                return;
            }

            var toRelease = _held & ~keys;
            if (toRelease != MoveKeys.None)
            {
                intent.Release(toRelease);
            }

            var toPress = keys & ~_held;
            if (toPress != MoveKeys.None)
            {
                intent.Press(toPress);
            }

            _held = keys;
        }

        private void CheckStuck(PlayerMotion motion)
        {
            if (motion.MovesSent != _lastSent || motion.PendingSteps > 0 || motion.IsResyncing)
            {
                _lastSent = motion.MovesSent;
                _lastSentAt = _clock.NowMs;
                return;
            }

            if (_clock.NowMs - _lastSentAt >= StuckMs)
            {
                Failure = "путь закрыт: персонаж стоит в " + motion.Position + " и не может шагнуть к " + GoalText() + ".";
            }
        }

        private string DescribePlanFailure(PathResult result, Point3D from)
        {
            switch (result.Status)
            {
                case PathStatus.OutOfBounds:
                    return "цель " + GoalText() + " за пределами карты.";
                case PathStatus.LimitReached:
                    return "путь не найден в пределе поиска (" + result.ExpandedNodes + " узлов) от " + from + " к " + GoalText() + ".";
                default:
                    return "путь закрыт: из " + from + " нет прохода к " + GoalText() + ".";
            }
        }

        private string GoalText() => _request.Z.HasValue
            ? "(" + _request.X + ", " + _request.Y + ", " + _request.Z.Value + ")"
            : "(" + _request.X + ", " + _request.Y + ")";

        private string Report(BotMovement movement) =>
            "Путь A* до " + GoalText() + (_request.Run ? ", бег" : ", шаг") + ": шагов " +
            (movement.Motion.MovesSent - _sentBefore) + ", перестроений " + _replans + ", отказов сервера " +
            (movement.Replies.RejectCount - _rejectsBefore) + ", ресинхронизаций " +
            (movement.Replies.ResyncCount - _resyncsBefore) + ".";
    }
}
