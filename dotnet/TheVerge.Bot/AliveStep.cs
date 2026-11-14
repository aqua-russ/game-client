using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// alive (PLAN Стадия 4, п. 9): ждать, что сервер снова показывает свой персонаж живым — тело не призрака и здоровье
    /// больше 0 (после [res: Mobile.Resurrect шлёт тело и здоровье обычными пакетами; отдельного «жив» нет).
    /// Срок — <see cref="BotScript.DeathTimeoutMs"/>.
    /// </summary>
    internal sealed class AliveStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;

        public AliveStep(BotWorld world, ILog log)
        {
            _world = world;
            _log = log;
        }

        public int TimeoutMs => BotScript.DeathTimeoutMs;

        public string TimeoutProblem => "сервер не вернул живое тело и здоровье за " + BotScript.DeathTimeoutMs / 1000 + " с.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (player == null || player.IsGhost || player.Status.Hits.Current <= 0)
            {
                return false;
            }

            _log.Info("Жив: тело 0x" + player.Body.ToString("X4") + ", здоровье " + player.Status.Hits + ".");
            return true;
        }

        public void Finish()
        {
        }
    }
}
