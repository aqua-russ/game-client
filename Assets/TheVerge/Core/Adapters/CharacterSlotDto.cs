namespace TheVerge.Core
{
    /// <summary>
    /// Описание слота персонажа на аккаунте из серверного пакета 0xA9.
    /// </summary>
    public readonly struct CharacterSlotDto
    {
        public int SlotIndex { get; }
        public string Name { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Name);

        public CharacterSlotDto(int slotIndex, string name)
        {
            SlotIndex = slotIndex;
            Name = name?.Trim() ?? string.Empty;
        }
    }
}
