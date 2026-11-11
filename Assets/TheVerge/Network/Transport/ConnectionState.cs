namespace TheVerge.Network.Transport
{
    /// <summary>Состояние соединения. Одно соединение — одно подключение: после Closed создаётся новое.</summary>
    public enum ConnectionState : byte
    {
        Idle = 0,
        Connecting = 1,
        Connected = 2,
        Closed = 3,
    }
}
