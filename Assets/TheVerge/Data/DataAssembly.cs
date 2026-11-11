namespace TheVerge.Data
{
    /// <summary>
    /// Сборка чтения файлов данных UO (карта, статика, tiledata, multi, cliloc) — наполняется на Стадии 2.
    /// Пока здесь только этот тип: без скриптов Unity не компилирует сборку из .asmdef,
    /// а бот и тесты по нему проверяют, что сборка подключена.
    /// </summary>
    public static class DataAssembly
    {
        public const string Name = "TheVerge.Data";
    }
}
