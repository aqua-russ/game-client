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
                case BotActionKind.SeenMobile:
                    return new SeenMobileStep(_world, _log, action.Argument);
                case BotActionKind.Dump:
                    return new DumpStep(_world, _log, _clock, action.Argument);
                case BotActionKind.StatusDump:
                    return new StatusDumpStep(_world, _log, _clock, action.Argument);
                case BotActionKind.MapDump:
                    MapDumpRequest.TryParse(action.Argument, out var request);
                    return new MapDumpStep(_world, _log, _clock, Data, request);
                case BotActionKind.UseItem:
                    return new UseItemStep(_world, _log, action.Argument);
                case BotActionKind.Bandage:
                    return new BandageStep(_world, _log);
                case BotActionKind.Fight:
                    return new FightStep(_world, _log, action.Argument);
                default:
                    return new OpenBackpackStep(_world, _log);
            }
        }
    }
}
