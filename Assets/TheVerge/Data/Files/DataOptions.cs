namespace TheVerge.Data.Files
{
    /// <summary>Настройки чтения файлов данных, которые сервер берёт из своих настроек, а не из самих файлов.</summary>
    public sealed class DataOptions
    {
        /// <summary>
        /// Запись multi.mul — 16 байт (флаги 64 бита, клиенты 7.0.9.0+) или 12 (32 бита). У сервера —
        /// maps.enablePostHSMultiComponentFormat, по умолчанию «версия клиента 7.0.9.0 или новее» (MultiData.cs,
        /// Configure); у нас 7.0.95.0 (TARGET.md §1), поэтому 16. MultiCollection.uop от этого не зависит.
        /// </summary>
        public bool MultiMulPostHsFormat { get; set; } = true;
    }
}
