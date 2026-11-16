using System.Collections.Generic;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// Действия бота в мире (--action) по очереди, после каждого тика. Каждое ждёт своего ответа сервера не дольше
    /// <see cref="ActionTimeoutMs"/> (бой — дольше, <see cref="IBotStep.TimeoutMs"/>); не дождалось — <see cref="Problem"/>,
    /// бот выходит с ошибкой.
    /// </summary>
    public sealed class BotScript
    {
        /// <summary>Сколько ждать ответа сервера на действие, мс.</summary>
        public const int ActionTimeoutMs = 10000;

        /// <summary>Сколько ждать конца боя (fight:), мс: крыса с 6 здоровья умирает за несколько ударов, но бой не мгновенный.</summary>
        public const int FightTimeoutMs = 60000;

        /// <summary>Сколько ждать своей смерти или воскрешения (dead, alive) и чужой смерти (deathof), мс: это делает GM-бот.</summary>
        public const int DeathTimeoutMs = 60000;

        /// <summary>Сколько ждать большого дампа ([DumpMove на 100 000 проверок): сервер считает и пишет его секунды.</summary>
        public const int LargeDumpTimeoutMs = 60000;

        /// <summary>
        /// Сколько ждать после [DumpVisible, прежде чем сверять: пакеты, которые сервер отправил до команды, должны дойти.
        /// </summary>
        public const int DumpSettleMs = 1000;

        private readonly IReadOnlyList<BotAction> _actions;
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly IClock _clock;
        private int _index;
        private IBotStep? _step;
        private long _startedAt;

        public BotScript(IReadOnlyList<BotAction> actions, BotWorld world, ILog log, IClock clock)
        {
            _actions = actions;
            _world = world;
            _log = log;
            _clock = clock;
        }

        public bool IsDone => _index >= _actions.Count;

        /// <summary>Файлы данных из --data — для mapdump; null — их нет.</summary>
        public BotWorldData? Data { get; set; }

        /// <summary>Почему действие не удалось; пусто — всё хорошо.</summary>
        public string Problem { get; private set; } = string.Empty;

        public void Step()
        {
            if (IsDone || Problem.Length > 0)
            {
                return;
            }

            if (_step == null)
            {
                _log.Info("Действие: " + _actions[_index].Text + ".");
                _step = Create(_actions[_index]);
                _startedAt = _clock.NowMs;
            }

            if (_step.Poll())
            {
                _step.Finish();
                _step = null;
                _index++;
            }
            else if (_step.Failure.Length > 0)
            {
                Problem = _actions[_index].Text + ": " + _step.Failure;
                _step.Finish();
                _step = null;
            }
            else if (_clock.NowMs - _startedAt >= _step.TimeoutMs)
            {
                Problem = _actions[_index].Text + ": " + _step.TimeoutProblem;
                _step.Finish();
                _step = null;
            }
        }

        private IBotStep Create(BotAction action)
        {
            switch (action.Kind)
            {
                case BotActionKind.Say:
                    return new SayStep(_world, action.Argument);
                case BotActionKind.WaitAt:
                    return new WaitAtStep(_world, _log, action.Argument);
                case BotActionKind.Target:
                    return new TargetStep(_world, _log, action.Argument);
                case BotActionKind.SeenItem:
                    return new SeenItemStep(_world, _log, action.Argument);
                case BotActionKind.NearItem:
                    return new NearItemStep(_world, _log, action.Argument);
                case BotActionKind.SeenMobile:
                    return new SeenMobileStep(_world, _log, action.Argument);
                case BotActionKind.Dump:
                    return new DumpStep(_world, _log, _clock, action.Argument);
                case BotActionKind.StatusDump:
                    return new StatusDumpStep(_world, _log, _clock, action.Argument);
                case BotActionKind.MapDump:
                    MapDumpRequest.TryParse(action.Argument, out var request);
                    return new MapDumpStep(_world, _log, _clock, Data, request);
                case BotActionKind.MoveDump:
                    MoveDumpRequest.TryParse(action.Argument, out var moveRequest);
                    return new MoveDumpStep(_world, _log, _clock, Data, moveRequest);
                case BotActionKind.OnMap:
                    return new OnMapStep(_world, _log, action.Argument);
                case BotActionKind.SeenMulti:
                    return new SeenMultiStep(_world, _log);
                case BotActionKind.Scene:
                    return new SceneStep(_world, _log, Data, action.Argument);
                case BotActionKind.Inspect:
                    return new InspectStep(_world, _log, Data, action.Argument);
                case BotActionKind.UseItem:
                    return new UseItemStep(_world, _log, action.Argument);
                default:
                    return CreateBodyStep(action);
            }
        }

        // Бой и смерть (Стадия 4); остальное — движение и рюкзак.
        private IBotStep CreateBodyStep(BotAction action)
        {
            switch (action.Kind)
            {
                case BotActionKind.Bandage:
                    return new BandageStep(_world, _log);
                case BotActionKind.Fight:
                    return new FightStep(_world, _log, action.Argument);
                case BotActionKind.Dead:
                    return new DeathStep(_world, _log);
                case BotActionKind.Alive:
                    return new AliveStep(_world, _log);
                case BotActionKind.DeathOf:
                    return new DeathOfStep(_world, _log, action.Argument);
                default:
                    return CreateMovementStep(action);
            }
        }

        // Движение своего персонажа (Стадия 3); остальное — рюкзак.
        private IBotStep CreateMovementStep(BotAction action)
        {
            switch (action.Kind)
            {
                case BotActionKind.Walk:
                    return new WalkStep(_world, _log, _clock, action.Argument);
                case BotActionKind.Mounted:
                    return new MountedStep(_world, _log);
                case BotActionKind.Goto:
                    return new GotoStep(_world, _log, _clock, action.Argument);
                case BotActionKind.Blocked:
                    return new BlockedStep(_world, _log, _clock, action.Argument);
                case BotActionKind.Resync:
                    return new ResyncStep(_world, _log);
                case BotActionKind.RandomWalk:
                    return new RandomWalkStep(_world, _log, _clock, action.Argument);
                case BotActionKind.WaitSteps:
                    return new WaitStepsStep(_world, _log, action.Argument);
                case BotActionKind.LagSpike:
                    return new LagSpikeStep(_world, _log, _clock, action.Argument);
                default:
                    return new OpenBackpackStep(_world, _log);
            }
        }
    }
}
