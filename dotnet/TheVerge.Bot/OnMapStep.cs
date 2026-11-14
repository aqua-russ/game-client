using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// onmap:N — ждать, пока мир бота не окажется на карте N: номер приходит от сервера (0xBF 0x08), например после
    /// GM-команды «[go Trammel» (PLAN Стадия 2, п. 12: смена карты).
    /// </summary>
    internal sealed class OnMapStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly int _map;

        public OnMapStep(BotWorld world, ILog log, string map)
        {
            _world = world;
            _log = log;
            BotAction.TryParseMap(map, out _map);
        }

        public string TimeoutProblem =>
            "мир не оказался на карте " + _map + " за " + BotScript.ActionTimeoutMs / 1000 + " с, " +
            (_world.Entities.MapIndex == EntityStore.UnknownMap ? "карта неизвестна." : "сейчас он на карте " + _world.Entities.MapIndex + ".");

        public string Failure => string.Empty;

        public bool Poll()
        {
            if (_world.Entities.MapIndex != _map)
            {
                return false;
            }

            _log.Info("Мир на карте " + _map + ".");
            return true;
        }

        public void Finish()
        {
        }
    }
}
