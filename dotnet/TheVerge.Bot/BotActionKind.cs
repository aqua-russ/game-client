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
        WaitAt
    }
}
