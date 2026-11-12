namespace TheVerge.Core.Session
{
    /// <summary>
    /// Версия клиента UO из четырёх чисел. По ней сервер выбирает формат пакетов (ProtocolChanges.cs); наша —
    /// docs/TARGET.md §1, в коде — <see cref="ServerLimits.ClientVersion"/>.
    /// </summary>
    public readonly struct ClientVersion
    {
        public ClientVersion(int major, int minor, int revision, int patch)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            Patch = patch;
        }

        public int Major { get; }

        public int Minor { get; }

        public int Revision { get; }

        public int Patch { get; }

        /// <summary>Как версию пишет клиент в ответе 0xBD и как её разбирает сервер: «7.0.95.0».</summary>
        public override string ToString() => Major + "." + Minor + "." + Revision + "." + Patch;
    }
}
