namespace TheVerge.Data.Files
{
    /// <summary>Итог поиска файлов данных: раскладка или понятная ошибка с именем файла (не исключение).</summary>
    public sealed class DataDetection
    {
        private DataDetection(DataLayout? layout, string error)
        {
            Layout = layout;
            Error = error;
        }

        public DataLayout? Layout { get; }

        /// <summary>Что не так, словами; пусто, если всё найдено.</summary>
        public string Error { get; }

        public bool IsValid => Layout != null;

        public static DataDetection Found(DataLayout layout) => new DataDetection(layout, string.Empty);

        public static DataDetection Failed(string error) => new DataDetection(null, error);
    }
}
