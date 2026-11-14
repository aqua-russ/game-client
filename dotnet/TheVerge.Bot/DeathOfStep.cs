using System.Linq;
using TheVerge.Core.Combat;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// deathof:0xТЕЛО (PLAN Стадия 4, п. 9): увидеть смерть ближайшего другого моба с этим телом — анимация смерти 0xAF
    /// (убитый, труп), труп в мире; в лог — вещи трупа по слоям (0x89; сервер шлёт их только для трупов людей, раньше 0xAF)
    /// и что стало с мобом: удалён (NPC) или призрак (игрок, если его видно — GM видит).
    /// </summary>
    internal sealed class DeathOfStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly ushort _body;
        private Serial _target;

        public DeathOfStep(BotWorld world, ILog log, string body)
        {
            _world = world;
            _log = log;
            BotAction.TryParseGraphic(body, out _body);
            Failure = world.Combat == null || world.Corpses == null ? "бой и трупы у бота не подключены." : string.Empty;
        }

        public int TimeoutMs => BotScript.DeathTimeoutMs;

        public string TimeoutProblem =>
            "анимации смерти (0xAF) " + _target + " с трупом в мире нет за " + BotScript.DeathTimeoutMs / 1000 + " с.";

        public string Failure { get; private set; }

        public bool Poll()
        {
            if (_world.Combat == null || _world.Corpses == null || _world.Entities.Player == null)
            {
                return false;
            }

            if (_target == Serial.Zero && !PickTarget())
            {
                return false;
            }

            var corpseSerial = DeathRecords.CorpseOf(_world.Combat.Log, _target);
            var corpse = corpseSerial == Serial.Zero ? null : _world.Entities.FindItem(corpseSerial);
            if (corpse == null)
            {
                return false;
            }

            var items = _world.Corpses.Get(corpse.Serial);
            var listed = !_world.Corpses.Has(corpse.Serial) ? "не приходили"
                : items.Count == 0 ? "нет" : string.Join(", ", items.Select(i => i.ToString()));
            _log.Info("Смерть " + _target + ": анимация смерти (0xAF), труп " + corpse.Serial + " (тело 0x" + corpse.Amount.ToString("X4") +
                      ") в " + corpse.Position + ", вещи по слоям (0x89): " + listed + "; " + Fate() + ".");
            return true;
        }

        public void Finish()
        {
        }

        /// <summary>
        /// Ближайший моб с телом; если его уже нет (сервер успел сменить тело или удалить моба, пока шёл прицел) — тот, чья
        /// смерть последней пришла в журнал боя.
        /// </summary>
        private bool PickTarget()
        {
            var mobile = NearbyMobiles.Nearest(_world.Entities, _body);
            _target = mobile?.Serial ?? LastDeath();
            if (_target == Serial.Zero)
            {
                Failure = "моба с телом 0x" + _body.ToString("X4") + " рядом нет, и смертей в журнале нет.";
                return false;
            }

            _log.Info("Жду смерти " + _target + (mobile != null ? " (тело 0x" + _body.ToString("X4") + ") в " + mobile.Position : " (по журналу боя)") + ".");
            return true;
        }

        private Serial LastDeath()
        {
            var journal = _world.Combat!.Log;
            for (var i = journal.Count - 1; i >= 0; i--)
            {
                if (journal[i].Kind == CombatEventKind.Death && journal[i].Source != _world.Entities.PlayerSerial)
                {
                    return journal[i].Source;
                }
            }

            return Serial.Zero;
        }

        private string Fate()
        {
            var mobile = _world.Entities.FindMobile(_target);
            return mobile == null ? "моб удалён"
                : mobile.IsGhost ? "моб стал призраком (тело 0x" + mobile.Body.ToString("X4") + ")"
                : "моб пока с прежним телом 0x" + mobile.Body.ToString("X4");
        }
    }
}
