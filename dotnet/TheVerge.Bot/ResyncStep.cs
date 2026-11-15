using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// resync (PLAN Стадия 3, п. 10; tools/check/scenes.sh, randomwalk.sh): дождаться ответов на все шаги, спросить сервер, где
    /// персонаж (0x22 → 0x20, Projects/UOContent/Network/Packets/IncomingPlayerPackets.cs, Resynchronize), и сверить его
    /// клетку и высоту с предсказанием клиента. Не совпало — действие не удалось: клиент считал шаги не так, как сервер.
    /// </summary>
    internal sealed class ResyncStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private bool _asked;
        private Point3D _predicted;

        public ResyncStep(BotWorld world, ILog log)
        {
            _world = world;
            _log = log;
        }

        public string TimeoutProblem => _asked
            ? "сервер не ответил на запрос, где персонаж, за " + BotScript.ActionTimeoutMs / 1000 + " с."
            : "сервер не ответил на шаги персонажа за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            var movement = _world.Movement;
            if (movement == null)
            {
                Failure = "нужны файлы данных (--data): без карты шаг не предсказать.";
                return false;
            }

            var motion = movement.Motion;
            if (!motion.IsPlaced || motion.IsResyncing || motion.PendingSteps > 0)
            {
                return false;
            }

            if (!_asked)
            {
                _predicted = motion.Position;
                movement.Replies.RequestResync();
                _asked = true;
                return false;
            }

            if (motion.Position != _predicted)
            {
                Failure = "сервер ставит персонажа в " + motion.Position + ", а клиент считал " + _predicted + ".";
                return false;
            }

            _log.Info("Сверка с сервером: персонаж в " + _predicted + ", как предсказано.");
            return true;
        }

        public void Finish()
        {
        }
    }
}
