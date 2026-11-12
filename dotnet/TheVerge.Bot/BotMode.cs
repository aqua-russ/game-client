namespace TheVerge.Bot
{
    public enum BotMode : byte
    {
        /// <summary>Подключиться к серверу.</summary>
        Run = 0,

        /// <summary>Проверить, что бот собран и запускается, без сервера (tools/check/all.sh).</summary>
        SelfCheck = 1,

        Help = 2,
    }
}
