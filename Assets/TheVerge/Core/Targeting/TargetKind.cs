namespace TheVerge.Core.Targeting
{
    /// <summary>Что выбрано прицелом: тип в ответе 0x6C (IncomingTargetingPackets.cs, TargetResponse: 1 — клетка).</summary>
    public enum TargetKind : byte
    {
        Object = 0,
        Location = 1
    }
}
