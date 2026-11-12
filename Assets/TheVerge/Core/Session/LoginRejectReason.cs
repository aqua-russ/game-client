namespace TheVerge.Core.Session
{
    /// <summary>
    /// Причина отказа логин-сервера — байт пакета отказа, значения сервера (OutgoingAccountPackets.cs, ALRReason).
    /// Неизвестный байт сохраняется как есть.
    /// </summary>
    public enum LoginRejectReason : byte
    {
        /// <summary>Нет такого аккаунта (и автосоздание выключено).</summary>
        Invalid = 0,

        /// <summary>Аккаунт уже в игре.</summary>
        InUse = 1,

        /// <summary>Аккаунт заблокирован.</summary>
        Blocked = 2,

        /// <summary>Неверный пароль.</summary>
        BadPassword = 3,

        /// <summary>Клиент слишком долго молчал.</summary>
        Idle = 254,

        /// <summary>Сервер отказал по другой причине: нельзя создать ещё аккаунт с этого адреса, очередь проверки полна, закрытый режим.</summary>
        BadCommunication = 255,
    }
}
