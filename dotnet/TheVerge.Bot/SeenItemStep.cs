using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>seen:0xГРАФИКА — ждать, пока на земле в клетке персонажа не появится предмет с этой графикой (после [add).</summary>
    internal sealed class SeenItemStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _graphic;
        private readonly List<Item> _found = new List<Item>();

        public SeenItemStep(BotWorld world, ILog log, string graphic)
        {
            _world = world;
            _log = log;
            BotAction.TryParseGraphic(graphic, out _graphic);
        }

        public string TimeoutProblem => "предмет 0x" + _graphic.ToString("X4") + " не появился под ногами за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (player == null)
            {
                return false;
            }

            _world.Entities.Spatial.QueryItems(player.Position.X, player.Position.Y, 0, _found);
            foreach (var item in _found)
            {
                if (item.Graphic == _graphic)
                {
                    _log.Info("Предмет 0x" + _graphic.ToString("X4") + " на земле: " + item.Serial + " в " + item.Position + ".");
                    return true;
                }
            }

            return false;
        }

        public void Finish()
        {
        }
    }
}
