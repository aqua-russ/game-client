using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// seen:0xГРАФИКА[,КОЛИЧЕСТВО] — ждать, пока на земле в клетке персонажа не появится предмет с этой графикой (после [add),
    /// а с количеством — стопка именно столько (например, бинтов после начала лечения).
    /// </summary>
    internal sealed class SeenItemStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _graphic;
        private readonly int _amount;
        private readonly List<Item> _found = new List<Item>();

        public SeenItemStep(BotWorld world, ILog log, string graphic)
        {
            _world = world;
            _log = log;
            TryParse(graphic, out _graphic, out _amount);
        }

        /// <summary>«0xГРАФИКА» или «0xГРАФИКА,КОЛИЧЕСТВО»; количество −1 — любое.</summary>
        public static bool TryParse(string text, out ushort graphic, out int amount)
        {
            var comma = text.IndexOf(',');
            amount = -1;
            if (comma < 0)
            {
                return BotAction.TryParseGraphic(text, out graphic);
            }

            return BotAction.TryParseGraphic(text.Substring(0, comma), out graphic) &&
                   int.TryParse(text.Substring(comma + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out amount);
        }

        public string TimeoutProblem => "предмет 0x" + _graphic.ToString("X4") + (_amount >= 0 ? " в количестве " + _amount : string.Empty) +
                                         " не появился под ногами за " + BotScript.ActionTimeoutMs / 1000 + " с.";

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
                if (item.Graphic == _graphic && (_amount < 0 || item.Amount == _amount))
                {
                    _log.Info("Предмет 0x" + _graphic.ToString("X4") + " на земле: " + item.Serial + " в " + item.Position +
                              (_amount >= 0 ? ", количество " + item.Amount : string.Empty) + ".");
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
