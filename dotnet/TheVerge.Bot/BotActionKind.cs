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
        SeenItem
    }
}
