using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// target:here|self|x,y,z|cancel — дождаться курсора сервера и ответить: клетка под ногами (земля), свой персонаж,
    /// клетка x,y,z (земля) или отмена. Курсор принимает только объект, а нужна клетка — ошибка сразу.
    /// </summary>
    internal sealed class TargetStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly string _argument;

        public TargetStep(BotWorld world, ILog log, string argument)
        {
            _world = world;
            _log = log;
            _argument = argument;
        }

        public string TimeoutProblem => "сервер не открыл курсор за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            var cursor = _world.Target;
            if (!cursor.IsActive)
            {
                return false;
            }

            var player = _world.Entities.Player;
            var sent = _argument == "cancel" ? cursor.Cancel()
                : _argument == "self" ? player != null && cursor.TargetObject(player.Serial)
                : TargetCell(player);
            if (sent)
            {
                _log.Info("Прицел: ответ «" + _argument + "» на курсор " + cursor.CursorId + ".");
            }
            else if (Failure.Length == 0)
            {
                Failure = "ответить нечем (персонаж неизвестен).";
            }

            return sent;
        }

        public void Finish()
        {
        }

        private bool TargetCell(Mobile? player)
        {
            if (!_world.Target.AllowsLocation)
            {
                Failure = "курсор сервера принимает только объект, а не клетку.";
                return false;
            }

            Point3D point;
            if (_argument == "here")
            {
                if (player == null)
                {
                    return false;
                }

                point = player.Position;
            }
            else
            {
                BotAction.TryParsePoint(_argument, out point);
            }

            return _world.Target.TargetLocation(point, 0);
        }
    }
}
