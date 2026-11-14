using TheVerge.Core.Primitives;

namespace TheVerge.Core.Combat
{
    /// <summary>
    /// Одно событие журнала боя (PLAN Стадия 4, п. 8): кто (<see cref="Source"/>), что (<see cref="Kind"/>, <see cref="Value"/>),
    /// по кому (<see cref="Target"/>), когда (<see cref="TimeMs"/> — часы клиента). Чего у события нет — ноль.
    /// Заполняет только <see cref="CombatLog"/>: ячейки переиспользуются, новой памяти нет.
    /// </summary>
    public struct CombatEvent
    {
        public long TimeMs { get; internal set; }

        public CombatEventKind Kind { get; internal set; }

        public Serial Source { get; internal set; }

        public Serial Target { get; internal set; }

        /// <summary>Действие анимации, урон, графика эффекта (у экранного — его номер), номер звука.</summary>
        public int Value { get; internal set; }

        /// <summary>Вид эффекта (<see cref="EffectKind"/>); у остальных — 0.</summary>
        public int Detail { get; internal set; }

        /// <summary>Точка звука или начальная точка эффекта.</summary>
        public Point3D Position { get; internal set; }

        /// <summary>Цвет эффекта (0xC0, 0xC7).</summary>
        public int Hue { get; internal set; }
    }
}
