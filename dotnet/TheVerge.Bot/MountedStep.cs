using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// mounted — ждать, что свой персонаж верхом: на нём предмет слоя верховой езды (Layer.Mount, 0x19). Посадка на
    /// эфирного коня — заклинание на 2 с (Ethereals.cs, EtherealSpell), во время каста сервер шаг не принимает.
    /// </summary>
    internal sealed class MountedStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;

        public MountedStep(BotWorld world, ILog log)
        {
            _world = world;
            _log = log;
        }

        public string TimeoutProblem => "персонаж не сел верхом за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var mount = _world.Entities.Player?.GetEquipped(Layer.Mount);
            if (mount == null)
            {
                return false;
            }

            _log.Info("Верхом: 0x" + mount.Graphic.ToString("X4") + ".");
            return true;
        }

        public void Finish()
        {
        }
    }
}
