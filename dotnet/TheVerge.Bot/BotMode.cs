namespace TheVerge.Bot
{
    public enum BotMode : byte
    {
        /// <summary>Подключиться к серверу.</summary>
        Run = 0,

        /// <summary>Проверить, что бот собран и запускается, без сервера (tools/check/all.sh).</summary>
        SelfCheck = 1,

        Help = 2,

        /// <summary>Перевести запись посредника в лог пакетов (--convert-capture, tools/golden/record.sh).</summary>
        ConvertCapture = 3,

        /// <summary>Проверить лог шагов против карты и физики клиента (--check-walk, PLAN Стадия 3, п. 12).</summary>
        CheckWalk = 4,
    }
}
