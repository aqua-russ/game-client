using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>use:0xГРАФИКА — двойной клик по предмету рядом (сервер, например, откроет курсор бинта); ждёт задержку действий.</summary>
    internal sealed class UseItemStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _graphic;
        private readonly List<Item> _found = new List<Item>();

        public UseItemStep(BotWorld world, ILog log, string graphic)
        {
            _world = world;
            _log = log;
            BotAction.TryParseGraphic(graphic, out _graphic);
        }

        public string TimeoutProblem => "не удалось использовать предмет 0x" + _graphic.ToString("X4") + " за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure { get; private set; } = string.Empty;

        public bool Poll()
        {
            var item = NearbyItems.Find(_world.Entities, _graphic, _found);
            if (item == null)
            {
                Failure = "предмета 0x" + _graphic.ToString("X4") + " нет рядом (не дальше " + NearbyItems.Reach + " клеток).";
                return false;
            }

            if (!_world.Items.Use(item.Serial))
            {
                return false;
            }

            _log.Info("Двойной клик по предмету 0x" + _graphic.ToString("X4") + ": " + item.Serial + ".");
            return true;
        }

        public void Finish()
        {
        }
    }
}
