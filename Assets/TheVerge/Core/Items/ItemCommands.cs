using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.World;

namespace TheVerge.Core.Items
{
    /// <summary>
    /// Действия игрока с предметами (PLAN Стадия 2, п. 9а): двойной клик и «открыть свой рюкзак». Сервер выполняет
    /// двойной клик, только если с прошлого действия прошла задержка (IncomingEntityPackets.cs, UseReq; actionDelay —
    /// <see cref="ServerLimits.ActionDelayMs"/>), поэтому раньше времени клиент его не шлёт.
    /// </summary>
    public sealed class ItemCommands
    {
        private readonly EntityStore _store;
        private readonly IItemSender _sender;
        private readonly IClock _clock;
        private readonly ActionTimer _actions;

        /// <param name="actions">Задержка действий, общая с быстрым бинтом (Стадия 4); null — своя.</param>
        public ItemCommands(EntityStore store, IItemSender sender, IClock clock, ActionTimer? actions = null)
        {
            _store = store;
            _sender = sender;
            _clock = clock;
            _actions = actions ?? new ActionTimer();
        }

        /// <summary>Двойной клик по объекту. false — не отправлен: не тот серийник или не прошла задержка действий.</summary>
        public bool Use(Serial serial)
        {
            if (!serial.IsMobile && !serial.IsItem)
            {
                return false;
            }

            if (!_actions.TryStart(_clock.NowMs))
            {
                return false;
            }

            _sender.SendUse(serial);
            return true;
        }

        /// <summary>Открыть рюкзак своего персонажа (предмет на слое Backpack). false — рюкзака не видно или рано.</summary>
        public bool OpenBackpack()
        {
            var backpack = _store.Player?.GetEquipped(Layer.Backpack);
            return backpack != null && Use(backpack.Serial);
        }
    }
}
