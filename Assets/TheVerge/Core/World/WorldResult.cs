namespace TheVerge.Core.World
{
    /// <summary>Чем кончилось изменение мира. Всё, кроме <see cref="Applied"/>, — отказ: мир не изменился.</summary>
    public enum WorldResult : byte
    {
        Applied = 0,

        /// <summary>Серийник не того вида: моб вместо предмета, предмет вместо моба, 0 или выше 0x7FFFFFFF.</summary>
        InvalidSerial = 1,

        /// <summary>Слой экипировки вне Layer (0 или выше Bank).</summary>
        InvalidLayer = 2,

        /// <summary>Моб, на которого надевают предмет, неизвестен.</summary>
        UnknownParent = 3,

        /// <summary>Контейнер оказался бы внутри самого себя.</summary>
        Cycle = 4,

        /// <summary>Свой персонаж: удалять его клиенту сервер не шлёт.</summary>
        Protected = 5
    }
}
