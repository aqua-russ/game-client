using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>bandage:self — быстрый бинт (0xBF 0x2C) на себя бинтом 0x0E21 не дальше 2 клеток; ждёт задержку действий.</summary>
    internal sealed class BandageStep : IBotStep
    {
        public const ushort BandageGraphic = 0x0E21;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly List<Item> _found = new List<Item>();

        public BandageStep(BotWorld world, ILog log)
        {
            _world = world;
            _log = log;
            Failure = world.QuickTargets == null ? "быстрые цели у бота не подключены." : string.Empty;
        }

        public string TimeoutProblem => "быстрый бинт не отправлен за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure { get; private set; }

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (_world.QuickTargets == null || player == null)
            {
                return false;
            }

            var bandage = NearbyItems.Find(_world.Entities, BandageGraphic, _found);
            if (bandage == null)
            {
                Failure = "бинтов (0x0E21) нет рядом (не дальше " + NearbyItems.Reach + " клеток).";
                return false;
            }

            if (!_world.QuickTargets.Bandage(bandage.Serial, player.Serial))
            {
                return false;
            }

            _log.Info("Быстрый бинт " + bandage.Serial + " на себя.");
            return true;
        }

        public void Finish()
        {
        }
    }
}
