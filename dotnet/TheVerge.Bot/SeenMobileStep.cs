using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>seenmob:0xТЕЛО — ждать, пока в мире бота не появится чужой моб с этим телом (после [add, например крыса 0x00EE).</summary>
    internal sealed class SeenMobileStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _body;

        public SeenMobileStep(BotWorld world, ILog log, string body)
        {
            _world = world;
            _log = log;
            BotAction.TryParseGraphic(body, out _body);
        }

        public string TimeoutProblem => "моб с телом 0x" + _body.ToString("X4") + " не появился за " + BotScript.ActionTimeoutMs / 1000 + " с.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var player = _world.Entities.PlayerSerial;
            foreach (var mobile in _world.Entities.Mobiles)
            {
                if (mobile.Serial != player && mobile.Body == _body)
                {
                    _log.Info("Моб с телом 0x" + _body.ToString("X4") + ": " + mobile.Serial + " в " + mobile.Position + ".");
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
