namespace TheVerge.Core.Combat
{
    /// <summary>Что произошло в бою (PLAN Стадия 4, п. 8): по пакету сервера.</summary>
    public enum CombatEventKind : byte
    {
        /// <summary>0x2F: замах — кто (<see cref="CombatEvent.Source"/>) по кому (<see cref="CombatEvent.Target"/>). Сервер шлёт только атакующему.</summary>
        Swing,

        /// <summary>0x6E: анимация моба (<see cref="CombatEvent.Source"/>), номер действия — <see cref="CombatEvent.Value"/>.</summary>
        Animation,

        /// <summary>0x0B: урон мобу (<see cref="CombatEvent.Target"/>), сколько — <see cref="CombatEvent.Value"/>.</summary>
        Damage,

        /// <summary>0x70, 0xC0, 0xC7: эффект; вид — <see cref="CombatEvent.Detail"/> (<see cref="EffectKind"/>), графика — <see cref="CombatEvent.Value"/>.</summary>
        Effect,

        /// <summary>0x54: звук <see cref="CombatEvent.Value"/> в точке <see cref="CombatEvent.Position"/>.</summary>
        Sound,
    }
}
