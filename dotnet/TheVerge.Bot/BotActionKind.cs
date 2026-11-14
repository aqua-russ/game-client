namespace TheVerge.Bot
{
    /// <summary>Виды действий бота в мире (--action).</summary>
    public enum BotActionKind
    {
        /// <summary>use:backpack — открыть свой рюкзак и написать его содержимое.</summary>
        UseBackpack,
        /// <summary>use:0xГРАФИКА — двойной клик по предмету с этой графикой не дальше 2 клеток.</summary>
        UseItem,
        /// <summary>bandage:self — быстрый бинт на себя бинтом не дальше 2 клеток (0xBF 0x2C).</summary>
        Bandage,
        /// <summary>fight:0xТЕЛО — боевая стойка, атака ближайшего моба с этим телом, ждать его смерти (Стадия 4, п. 7).</summary>
        Fight,
        /// <summary>dead — ждать своей смерти по серверу: 0x2C, тело призрака, свой труп с вещами по слоям (Стадия 4, п. 9).</summary>
        Dead,
        /// <summary>alive — ждать, что сервер снова показывает живое тело и здоровье (после [res).</summary>
        Alive,
        /// <summary>deathof:0xТЕЛО — увидеть смерть ближайшего другого моба с этим телом: 0xAF, труп, вещи по слоям.</summary>
        DeathOf,

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
