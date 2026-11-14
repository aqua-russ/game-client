using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Targeting
{
    /// <summary>
    /// Быстрые цели без курсора (PLAN Стадия 4, п. 6; сервер — IncomingExtendedCommandPackets.cs): бинт на цель (сразу
    /// лечит, не чаще задержки действий — общей с двойным кликом), заклинание (сервер цель не использует — начинает каст,
    /// дальше обычный курсор: Spellbook.TargetedSpell), навык на цель (PlayerMobile.TargetedSkillUse), по ресурсу (только
    /// предмет). Чего сервер не примет, клиент не шлёт.
    /// </summary>
    public sealed class TargetCommands
    {
        private readonly IQuickTargetSender _sender;
        private readonly IClock _clock;
        private readonly ActionTimer _actions;

        public TargetCommands(IQuickTargetSender sender, IClock clock, ActionTimer actions)
        {
            _sender = sender;
            _clock = clock;
            _actions = actions;
        }

        /// <summary>Бинт <paramref name="bandage"/> (предмет) на моба <paramref name="target"/>; false — не тот серийник или рано.</summary>
        public bool Bandage(Serial bandage, Serial target)
        {
            if (!bandage.IsItem || !target.IsMobile || !_actions.TryStart(_clock.NowMs))
            {
                return false;
            }

            _sender.SendBandageTarget(bandage, target);
            return true;
        }

        /// <summary>Заклинание по номеру книги (с 1) на цель.</summary>
        public bool CastAt(int spellNumber, Serial target)
        {
            if (spellNumber < 1 || spellNumber > short.MaxValue || !IsEntity(target))
            {
                return false;
            }

            _sender.SendTargetedSpell(spellNumber, target);
            return true;
        }

        /// <summary>Навык по номеру (с 0) на цель.</summary>
        public bool UseSkillAt(int skillId, Serial target)
        {
            if (skillId < 0 || skillId > short.MaxValue || !IsEntity(target))
            {
                return false;
            }

            _sender.SendTargetedSkill(skillId, target);
            return true;
        }

        /// <summary>Цель по ресурсу: предмет и вид ресурса (сервер передаёт их системе крафта).</summary>
        public bool TargetByResource(Serial item, int resourceType)
        {
            if (!item.IsItem || resourceType < short.MinValue || resourceType > short.MaxValue)
            {
                return false;
            }

            _sender.SendTargetByResource(item, resourceType);
            return true;
        }

        private static bool IsEntity(Serial serial) => serial.IsMobile || serial.IsItem;
    }
}
