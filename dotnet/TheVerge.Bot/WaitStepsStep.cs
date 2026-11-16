using System.Globalization;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// waitsteps:&lt;0xТЕЛО&gt;,&lt;количество&gt; (PLAN Стадия 3, п. 12): ждать, пока чужой моб с этим телом начнёт
    /// столько шагов в <see cref="OthersMotion"/> — чтобы запускать всплеск лага ровно во время его движения.
    /// </summary>
    internal sealed class WaitStepsStep : IBotStep
    {
        private const int SlackMs = 15000;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _body;
        private readonly int _target;
        private OthersMotion? _subscribed;
        private int _seen;

        public WaitStepsStep(BotWorld world, ILog log, string argument)
        {
            _world = world;
            _log = log;
            TryParse(argument, out _body, out _target);
        }

        public int TimeoutMs => System.Math.Max(BotScript.ActionTimeoutMs, _target * StepTiming.StepDelayMs(false, false) + SlackMs);

        public string TimeoutProblem =>
            "моб 0x" + _body.ToString("X4") + " сделал " + _seen + " шагов из " + _target + " за " + TimeoutMs / 1000 + " с.";

        public string Failure { get; private set; } = string.Empty;

        public static bool TryParse(string argument, out ushort body, out int count)
        {
            body = 0;
            count = 0;
            var parts = argument.Split(',');
            return parts.Length == 2 &&
                   BotAction.TryParseGraphic(parts[0], out body) &&
                   int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out count) &&
                   count > 0;
        }

        public bool Poll()
        {
            var others = _world.Others?.Motion;
            if (others == null)
            {
                Failure = "показ чужих мобов не подключён.";
                return false;
            }

            if (_subscribed == null)
            {
                _subscribed = others;
                _subscribed.StepStarted += OnStepStarted;
            }

            if (_seen < _target)
            {
                return false;
            }

            _log.Info("Увидено шагов моба 0x" + _body.ToString("X4") + ": " + _seen + ".");
            return true;
        }

        public void Finish()
        {
            if (_subscribed != null)
            {
                _subscribed.StepStarted -= OnStepStarted;
                _subscribed = null;
            }
        }

        private void OnStepStarted(Serial serial, ShownStep step)
        {
            var mobile = _world.Entities.FindMobile(serial);
            if (mobile != null && mobile.Body == _body)
            {
                _seen++;
            }
        }
    }
}