namespace TheVerge.Presentation
{
    /// <summary>
    /// Метка сборки Presentation (ввод, камера, отрисовка, UI). Старый UI прототипа удалён в задаче 0.9 (ADR 0011);
    /// пока новых окон нет, без скриптов Unity не собрал бы сборку из .asmdef, а на неё ссылаются Bootstrap и Editor.
    /// Удалить, когда в сборке появится первый настоящий класс (задача 1.16).
    /// </summary>
    public static class PresentationAssembly
    {
        public const string Name = "TheVerge.Presentation";
    }
}
