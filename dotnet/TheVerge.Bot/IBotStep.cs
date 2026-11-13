namespace TheVerge.Bot
{
    /// <summary>Выполнение одного действия бота: шаг после каждого тика, пока не готово.</summary>
    internal interface IBotStep
    {
        /// <summary>Сделать, что можно сейчас; true — действие закончено.</summary>
        bool Poll();

        /// <summary>Что сказать, если ответа сервера не дождались.</summary>
        string TimeoutProblem { get; }

        /// <summary>Отписаться от событий мира.</summary>
        void Finish();
    }
}
