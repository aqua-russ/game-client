namespace TheVerge.Core.Chat
{
    /// <summary>Что стало с фразой.</summary>
    public enum SpeechResult : byte
    {
        /// <summary>Ушла в этом тике.</summary>
        Sent = 0,

        /// <summary>Ждёт своей очереди: с прошлой фразы не прошло 25 мс.</summary>
        Queued = 1,

        /// <summary>Пустая — сервер такую выбросит.</summary>
        Empty = 2,

        /// <summary>Длиннее 128 букв — сервер такую выбросит.</summary>
        TooLong = 3,

        /// <summary>Очередь полна — фраза не принята.</summary>
        QueueFull = 4
    }
}
