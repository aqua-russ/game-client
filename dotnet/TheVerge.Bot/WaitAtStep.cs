using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>at:x,y — ждать, пока свой персонаж не окажется в клетке (после [go, телепорта).</summary>
    internal sealed class WaitAtStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly short _x;
        private readonly short _y;

        public WaitAtStep(BotWorld world, ILog log, string cell)
        {
            _world = world;
            _log = log;
            BotAction.TryParseCell(cell, out _x, out _y);
        }

        public string TimeoutProblem =>
            "персонаж не оказался в клетке (" + _x + ", " + _y + ") за " + BotScript.ActionTimeoutMs / 1000 + " с, он в " +
            (_world.Entities.Player?.Position.ToString() ?? "неизвестно где") + ".";

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (player == null || player.Position.X != _x || player.Position.Y != _y)
            {
                return false;
            }

            _log.Info("Персонаж в клетке " + player.Position + ".");
            return true;
        }

        public void Finish()
        {
        }
    }
}
