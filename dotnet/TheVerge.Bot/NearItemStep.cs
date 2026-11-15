using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// near:0xГРАФИКА — ждать предмет с этой графикой на земле не дальше 2 клеток (tools/check/scenes.sh: дверь открылась или
    /// закрылась — у двери другая графика; без ожидания следующий шаг ушёл бы раньше, чем клиент узнал о двери).
    /// </summary>
    internal sealed class NearItemStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _graphic;
        private readonly List<Item> _found = new List<Item>();

        public NearItemStep(BotWorld world, ILog log, string graphic)
        {
            _world = world;
            _log = log;
            BotAction.TryParseGraphic(graphic, out _graphic);
        }

        public string TimeoutProblem =>
            "предмет 0x" + _graphic.ToString("X4") + " так и не появился рядом (не дальше " + NearbyItems.Reach + " клеток) за " +
            BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var item = NearbyItems.Find(_world.Entities, _graphic, _found);
            if (item == null)
            {
                return false;
            }

            _log.Info("Рядом предмет 0x" + _graphic.ToString("X4") + ": " + item.Serial + " в " + item.Position + ".");
            return true;
        }

        public void Finish()
        {
        }
    }
}
