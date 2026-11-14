namespace TheVerge.Bot
{
    /// <summary>Выполнение одного действия бота: шаг после каждого тика, пока не готово.</summary>
    internal interface IBotStep
    {
        /// <summary>Сделать, что можно сейчас; true — действие закончено.</summary>
        bool Poll();

        /// <summary>Что сказать, если ответа сервера не дождались.</summary>
        string TimeoutProblem { get; }

        /// <summary>Действие невозможно (не ждать конца времени); пусто — всё идёт своим чередом.</summary>
        string Failure { get; }

        /// <summary>Сколько ждать ответа сервера, мс; обычно <see cref="BotScript.ActionTimeoutMs"/>, бою нужно дольше.</summary>
        int TimeoutMs => BotScript.ActionTimeoutMs;

        /// <summary>Отписаться от событий мира.</summary>
        void Finish();
    }
}
