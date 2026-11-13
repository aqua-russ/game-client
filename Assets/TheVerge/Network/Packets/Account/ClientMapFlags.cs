namespace TheVerge.Network.Packets.Account
{
    /// <summary>
    /// Флаги карт клиента в 0x5D и 0xF8 (сервер: Projects/Server/ExpansionInfo.cs, ClientFlags; хранит в NetState.Flags).
    /// По ним сервер решает, куда ведут лунные врата (UOContent/Items/Misc/PublicMoongate.cs): с флагом Trammel в UOR —
    /// и в Траммель. У нас только Фелукка (docs/TARGET.md §1), поэтому только флаг Felucca.
    /// </summary>
    internal static class ClientMapFlags
    {
        public const int FeluccaOnly = 0x01;
    }
}
