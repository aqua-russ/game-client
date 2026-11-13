using System.Collections.Generic;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// Действия бота в мире (--action) по очереди, после каждого тика. Каждое ждёт своего ответа сервера не дольше
    /// <see cref="ActionTimeoutMs"/>; не дождалось — <see cref="Problem"/>, бот выходит с ошибкой.
    /// </summary>
    public sealed class BotScript
    {
        /// <summary>Сколько ждать ответа сервера на действие, мс.</summary>
        public const int ActionTimeoutMs = 10000;

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
            else if (_clock.NowMs - _startedAt >= ActionTimeoutMs)
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
                default:
                    return new OpenBackpackStep(_world, _log);
            }
        }
    }
}
