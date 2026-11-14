namespace TheVerge.Core.World
{
    /// <summary>
    /// Состояние моба по пакетам сервера (PLAN Стадия 4, п. 4): имя, здоровье, мана, выносливость, полный статус своего
    /// персонажа, яд и «жёлтая» полоска (неуязвимость), мёртвый привязанный питомец, замки статов. Клиент ничего не
    /// вычисляет сам: только то, что прислал сервер. Меняет <see cref="Status.MobileStatusUpdates"/>.
    /// </summary>
    public sealed class MobileStatus
    {
        public string Name { get; internal set; } = string.Empty;
        public Vital Hits { get; internal set; }
        public Vital Mana { get; internal set; }
        public Vital Stam { get; internal set; }

        /// <summary>Версия последнего 0x11: 0 — краткий (чужой моб), 3 — полный (свой в UOR); −1 — статуса не было.</summary>
        public int Version { get; internal set; } = -1;

        public bool CanBeRenamed { get; internal set; }
        public bool Female { get; internal set; }
        public StatusAttributes Attributes { get; internal set; }
        public int Followers { get; internal set; }
        public int FollowersMax { get; internal set; }

        /// <summary>0 — не отравлен; иначе уровень яда + 1, как шлёт сервер (CreateMobileHealthbar).</summary>
        public int PoisonLevel { get; internal set; }

        /// <summary>Жёлтая полоска здоровья: моб неуязвим (Blessed) или так решил сервер (YellowHealthbar).</summary>
        public bool YellowHealthbar { get; internal set; }

        public bool IsDeadBonded { get; internal set; }
        public StatLocks Locks { get; internal set; }
    }
}
