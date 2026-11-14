using System.Linq;
using TheVerge.Core.Ports;
using TheVerge.Core.World;

namespace TheVerge.Bot
{
    /// <summary>
    /// dead (PLAN Стадия 4, п. 9): ждать своей смерти — только по серверу: статус смерти 0x2C, тело призрака
    /// (<see cref="Mobile.IsGhost"/>), свой труп рядом (0x2006, количество = тело до смерти) со списком вещей по слоям (0x89;
    /// сервер шлёт его раньше 0x2C). Ждёт, пока GM не убьёт ([kill), — срок <see cref="BotScript.DeathTimeoutMs"/>.
    /// </summary>
    internal sealed class DeathStep : IBotStep
    {
        private const int CorpseReach = 2;

        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly System.Collections.Generic.List<Item> _items = new System.Collections.Generic.List<Item>();
        private bool _started;
        private int _deathsBefore;
        private ushort _bodyBefore;
        private bool _status;
        private bool _ghost;

        public DeathStep(BotWorld world, ILog log)
        {
            _world = world;
            _log = log;
            Failure = world.Combat == null || world.Corpses == null ? "бой и трупы у бота не подключены." : string.Empty;
        }

        public int TimeoutMs => BotScript.DeathTimeoutMs;

        public string TimeoutProblem =>
            (!_status ? "сервер не прислал статус смерти (0x2C)"
                : !_ghost ? "сервер не сделал тело призраком"
                : "своего трупа со списком вещей (0x89) рядом нет") + " за " + BotScript.DeathTimeoutMs / 1000 + " с.";

        public string Failure { get; private set; }

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (_world.Combat == null || _world.Corpses == null || player == null)
            {
                return false;
            }

            if (!_started)
            {
                _started = true;
                _deathsBefore = _world.Combat.State.Deaths;
                _bodyBefore = player.Body;
                _log.Info("Жду смерти: тело 0x" + _bodyBefore.ToString("X4") + ", здоровье " + player.Status.Hits + ".");
            }

            Note(ref _status, _world.Combat.State.Deaths > _deathsBefore, "Смерть: статус смерти (0x2C) от сервера.");
            Note(ref _ghost, player.IsGhost, "Смерть: тело призрака 0x" + player.Body.ToString("X4") + ", здоровье " + player.Status.Hits + ".");
            var corpse = _status && _ghost ? FindCorpse(player) : null;
            if (corpse == null)
            {
                return false;
            }

            var items = _world.Corpses.Get(corpse.Serial);
            var listed = items.Count == 0 ? "нет" : string.Join(", ", items.Select(i => Describe(i, corpse)));
            _log.Info("Смерть: свой труп " + corpse.Serial + " (тело 0x" + corpse.Amount.ToString("X4") + ") в " + corpse.Position +
                      ", вещи по слоям (0x89): " + listed + ".");
            return true;
        }

        public void Finish()
        {
        }

        private void Note(ref bool seen, bool now, string text)
        {
            if (!seen && now)
            {
                seen = true;
                _log.Info(text);
            }
        }

        private Item? FindCorpse(Mobile player)
        {
            _world.Entities.Spatial.QueryItems(player.Position.X, player.Position.Y, CorpseReach, _items);
            return _items.FirstOrDefault(i => i.Graphic == FightStep.CorpseGraphic && i.Amount == _bodyBefore && _world.Corpses!.Has(i.Serial));
        }

        private string Describe(CorpseItem item, Item corpse) =>
            item + (_world.Entities.FindItem(item.Item)?.Container == corpse ? " (в трупе)" : " (в трупе не видна)");
    }
}
