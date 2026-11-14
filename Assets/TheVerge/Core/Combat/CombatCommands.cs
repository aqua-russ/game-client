using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.Combat
{
    /// <summary>
    /// Боевые команды игрока (PLAN Стадия 4, п. 7): включить или выключить стойку (0x72) и атаковать моба (0x05). Итог —
    /// по ответу сервера в <see cref="CombatState"/>. Чего сервер не сделает (та же стойка, атака себя или не моба), не шлём.
    /// </summary>
    public sealed class CombatCommands
    {
        private readonly ICombatSender _sender;
        private readonly CombatState _state;
        private readonly EntityStore _store;

        public CombatCommands(ICombatSender sender, CombatState state, EntityStore store)
        {
            _sender = sender;
            _state = state;
            _store = store;
        }

        /// <summary>Попросить стойку; false — она уже такая (сервер на это не ответит).</summary>
        public bool SetWarMode(bool on)
        {
            if (on == _state.WarMode)
            {
                return false;
            }

            _sender.SendWarMode(on);
            return true;
        }

        /// <summary>Атаковать моба; false — не моб или свой персонаж (сервер себя противником не ставит).</summary>
        public bool Attack(Serial target)
        {
            if (!target.IsMobile || target == _store.PlayerSerial)
            {
                return false;
            }

            _sender.SendAttack(target);
            return true;
        }
    }
}
