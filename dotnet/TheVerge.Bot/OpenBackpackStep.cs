using System.Linq;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;

namespace TheVerge.Bot
{
    /// <summary>use:backpack — двойной клик по своему рюкзаку, дождаться его содержимого (0x24 и 0x3C) и записать его в лог.</summary>
    internal sealed class OpenBackpackStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private Serial _backpack;
        private bool _received;

        public OpenBackpackStep(BotWorld world, ILog log)
        {
            _world = world;
            _log = log;
            _world.Containers.ContentReceived += OnContent;
        }

        public string TimeoutProblem => _backpack.IsValid
            ? "сервер не открыл рюкзак за " + BotScript.ActionTimeoutMs / 1000 + " с."
            : "у персонажа не видно рюкзака: открыть нечего.";

        public string Failure => string.Empty;

        public bool Poll()
        {
            if (!_backpack.IsValid)
            {
                var backpack = _world.Entities.Player?.GetEquipped(Core.Primitives.Layer.Backpack);
                if (backpack != null && _world.Items.OpenBackpack())
                {
                    _backpack = backpack.Serial;
                }

                return false;
            }

            if (!_received)
            {
                return false;
            }

            var contents = _world.Entities.FindItem(_backpack)?.Contents;
            var list = contents == null ? string.Empty : string.Join(", ", contents.Select(i => "0x" + i.Graphic.ToString("X4") + "×" + i.Amount));
            _log.Info("Рюкзак " + _backpack + " открыт: предметов " + (contents?.Count ?? 0) + (list.Length > 0 ? " (" + list + ")" : string.Empty) + ".");
            return true;
        }

        public void Finish() => _world.Containers.ContentReceived -= OnContent;

        private void OnContent(Serial container) => _received |= _backpack.IsValid && container == _backpack;
    }
}
