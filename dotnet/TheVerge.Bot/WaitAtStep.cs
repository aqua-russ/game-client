using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>
    /// at:x,y[,z] — ждать, пока свой персонаж не окажется в клетке (после [go, телепорта); с высотой — ещё и на этой высоте
    /// (наверху лестницы, tools/check/scenes.sh).
    /// </summary>
    internal sealed class WaitAtStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly short _x;
        private readonly short _y;
        private readonly sbyte? _z;

        public WaitAtStep(BotWorld world, ILog log, string cell)
        {
            _world = world;
            _log = log;
            if (BotAction.TryParsePoint(cell, out var point))
            {
                _x = point.X;
                _y = point.Y;
                _z = point.Z;
            }
            else
            {
                BotAction.TryParseCell(cell, out _x, out _y);
            }
        }

        public string TimeoutProblem =>
            "персонаж не оказался в клетке (" + _x + ", " + _y + (_z.HasValue ? ", " + _z.Value : string.Empty) + ") за " +
            BotScript.ActionTimeoutMs / 1000 + " с, он в " + (_world.Entities.Player?.Position.ToString() ?? "неизвестно где") + ".";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (player == null || !IsHere(player.Position))
            {
                return false;
            }

            _log.Info("Персонаж в клетке " + player.Position + ".");
            return true;
        }

        public void Finish()
        {
        }

        private bool IsHere(Point3D position) =>
            position.X == _x && position.Y == _y && (!_z.HasValue || position.Z == _z.Value);
    }
}
