using System;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.Combat
{
    /// <summary>
    /// Стойка и противник своего персонажа — только по пакетам сервера (PLAN Стадия 4, п. 7): 0x72 — стойка
    /// (Mobile.Warmode → SendSetWarMode), 0xAA — противник, 0 — нет (Mobile.Combatant → SendChangeCombatant).
    /// Клиент их не предсказывает: сервер может отказать (частые переключения, мёртвый, нельзя бить).
    /// </summary>
    public sealed class CombatState
    {
        public event Action? WarModeChanged;

        public event Action? CombatantChanged;

        /// <summary>Сервер сообщил о своей смерти (0x2C, Mobile.OnDeath: SendDeathStatus) — экран смерти покажет Unity.</summary>
        public event Action? Died;

        public bool WarMode { get; private set; }

        public Serial Combatant { get; private set; }

        public bool HasCombatant => Combatant != Serial.Zero;

        /// <summary>Сколько раз за вход сервер сообщил о своей смерти (0x2C). Жив ли сейчас — по телу (<see cref="World.Mobile.IsGhost"/>).</summary>
        public int Deaths { get; private set; }

        public void OnWarMode(bool warMode)
        {
            if (WarMode != warMode)
            {
                WarMode = warMode;
                WarModeChanged?.Invoke();
            }
        }

        public void OnCombatant(Serial combatant)
        {
            if (Combatant != combatant)
            {
                Combatant = combatant;
                CombatantChanged?.Invoke();
            }
        }

        /// <summary>0x2C: свой персонаж умер. Ответ клиента сервер игнорирует (DeathStatusResponse) — не шлём.</summary>
        public void OnDeathStatus()
        {
            Deaths++;
            Died?.Invoke();
        }

        /// <summary>Новый вход: стойку и противника сервер пришлёт заново (IncomingAccountPackets: SendSetWarMode при входе).</summary>
        public void Reset()
        {
            WarMode = false;
            Combatant = Serial.Zero;
            Deaths = 0;
        }
    }
}
