namespace TheVerge.Core.Session
{
    /// <summary>Слот персонажа в списке игрового сервера. Пустой слот — пустое имя.</summary>
    public readonly struct CharacterSlot
    {
        public CharacterSlot(int slot, string name)
        {
            Slot = slot;
            Name = name;
        }

        /// <summary>Номер слота: его сервер ждёт при выборе и удалении персонажа.</summary>
        public int Slot { get; }

        public string Name { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Name);

        public override string ToString() => IsEmpty ? "(пусто)" : Name;
    }
}
