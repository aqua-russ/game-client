namespace TheVerge.Bot
{
    /// <summary>Виды действий бота в мире (--action).</summary>
    public enum BotActionKind
    {
        /// <summary>use:backpack — открыть свой рюкзак и написать его содержимое.</summary>
        UseBackpack,

        /// <summary>say:&lt;текст&gt; — сказать (и GM-команды: «[go 1500 1600»).</summary>
        Say,

        /// <summary>at:x,y — ждать, пока свой персонаж не окажется в этой клетке.</summary>
        WaitAt,

        /// <summary>target:here|self|x,y,z|cancel — ответить на курсор сервера (после [add, [remove …).</summary>
        Target,

        /// <summary>seen:0xГРАФИКА — ждать предмет с этой графикой на земле в клетке персонажа.</summary>
        SeenItem,
        /// <summary>seenmob:0xТЕЛО — ждать чужого моба с этим телом (после [add).</summary>
        SeenMobile,

        /// <summary>dump:&lt;папка&gt; — сказать [DumpVisible, дождаться файла сервера в папке и сверить свой мир с ним.</summary>
        Dump,
        /// <summary>
        /// mapdump:&lt;клеток&gt;,&lt;seed&gt;,&lt;папка&gt;[,all-statics] — сказать [DumpMapData, дождаться файла сервера и сверить
        /// с ним свои файлы данных (--data): клетки, tiledata, дома и корабли.
        /// </summary>
        MapDump,

        /// <summary>
        /// statusdump:&lt;папка&gt; — запросить статус видимых мобов, сказать [DumpMobile, дождаться файла сервера и сверить
        /// с ним состояние мобов бота (Стадия 4, п. 4).
        /// </summary>
        StatusDump
    }
}
