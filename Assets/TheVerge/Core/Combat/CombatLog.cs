using System;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.Combat
{
    /// <summary>
    /// Журнал боя (PLAN Стадия 4, п. 8–9): замахи, анимации, урон, эффекты, звуки и смерти в том порядке, в каком их прислал
    /// сервер.
    /// Кольцо фиксированного размера: полное — старое вытесняется; ячейки переиспользуются — новой памяти не берёт.
    /// Окно журнала в Unity — задача 4.11.
    /// </summary>
    public sealed class CombatLog
    {
        private readonly CombatEvent[] _events;
        private readonly IClock _clock;
        private int _first;

        public CombatLog(IClock clock, int capacity = ClientLimits.CombatLogCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _clock = clock;
            _events = new CombatEvent[capacity];
        }

        /// <summary>Новое событие (уже в журнале).</summary>
        public event Action<CombatEvent>? Added;

        public int Capacity => _events.Length;

        /// <summary>Сколько событий в журнале сейчас (не больше <see cref="Capacity"/>).</summary>
        public int Count { get; private set; }

        /// <summary>Сколько событий пришло всего (с вытесненными; <see cref="Clear"/> не сбрасывает).</summary>
        public long Total { get; private set; }

        /// <summary>Событие по порядку: 0 — самое старое из оставшихся.</summary>
        public CombatEvent this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _events[(_first + index) % _events.Length];
            }
        }

        public void OnSwing(Serial attacker, Serial defender)
        {
            ref var e = ref Next(CombatEventKind.Swing);
            e.Source = attacker;
            e.Target = defender;
            Publish(e);
        }

        public void OnAnimation(Serial mobile, int action)
        {
            ref var e = ref Next(CombatEventKind.Animation);
            e.Source = mobile;
            e.Value = action;
            Publish(e);
        }

        public void OnDamage(Serial mobile, int amount)
        {
            ref var e = ref Next(CombatEventKind.Damage);
            e.Target = mobile;
            e.Value = amount;
            Publish(e);
        }

        public void OnEffect(EffectKind kind, Serial source, Serial target, int itemId, Point3D position, int hue)
        {
            ref var e = ref Next(CombatEventKind.Effect);
            e.Detail = (int)kind;
            e.Source = source;
            e.Target = target;
            e.Value = itemId;
            e.Position = position;
            e.Hue = hue;
            Publish(e);
        }

        public void OnScreenEffect(int type)
        {
            ref var e = ref Next(CombatEventKind.Effect);
            e.Detail = (int)EffectKind.Screen;
            e.Value = type;
            Publish(e);
        }

        public void OnSound(ushort sound, Point3D position)
        {
            ref var e = ref Next(CombatEventKind.Sound);
            e.Value = sound;
            e.Position = position;
            Publish(e);
        }

        public void OnDeath(Serial killed, Serial corpse)
        {
            ref var e = ref Next(CombatEventKind.Death);
            e.Source = killed;
            e.Target = corpse;
            Publish(e);
        }

        /// <summary>Новый вход: журнал пуст (<see cref="Total"/> остаётся).</summary>
        public void Clear()
        {
            _first = 0;
            Count = 0;
        }

        private ref CombatEvent Next(CombatEventKind kind)
        {
            int slot;
            if (Count < _events.Length)
            {
                slot = (_first + Count) % _events.Length;
                Count++;
            }
            else
            {
                slot = _first;
                _first = (_first + 1) % _events.Length;
            }

            Total++;
            ref var e = ref _events[slot];
            e = default;
            e.TimeMs = _clock.NowMs;
            e.Kind = kind;
            return ref e;
        }

        private void Publish(in CombatEvent e) => Added?.Invoke(e);
    }
}
