using System;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// seenmulti: — ждать дом или корабль (multi, 0xF3 типа 2) в пределах обзора multi (квадрат, как у сервера:
    /// <see cref="ServerLimits.MultiUpdateRange"/>). Части дома и корабля приходят отдельными предметами сразу следом.
    /// </summary>
    internal sealed class SeenMultiStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;

        public SeenMultiStep(BotWorld world, ILog log)
        {
            _world = world;
            _log = log;
        }

        public string TimeoutProblem =>
            "дом или корабль (multi) не появился в пределах " + ServerLimits.MultiUpdateRange + " клеток за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (player == null)
            {
                return false;
            }

            foreach (var item in _world.Entities.Items)
            {
                if (item.IsMulti && item.Place == ItemPlace.World && InRange(player, item))
                {
                    _log.Info("Дом или корабль (multi): " + item.Serial + " в " + item.Position + ".");
                    return true;
                }
            }

            return false;
        }

        public void Finish()
        {
        }

        private static bool InRange(Mobile player, Item item) =>
            Math.Abs(item.Position.X - player.Position.X) <= ServerLimits.MultiUpdateRange &&
            Math.Abs(item.Position.Y - player.Position.Y) <= ServerLimits.MultiUpdateRange;
    }
}
