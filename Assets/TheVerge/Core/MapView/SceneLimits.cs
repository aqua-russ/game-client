namespace TheVerge.Core.MapView
{
    /// <summary>Размеры отладочной карты в единицах Z сервера (1 Z = 0,1 м в сцене — задаёт Presentation).</summary>
    public static class SceneLimits
    {
        /// <summary>Минимальная толщина блока земли: самая низкая клетка всё равно видна как столбик.</summary>
        public const int LandMinThickness = 2;

        /// <summary>Минимальная толщина статики и части дома: высота 0 по tiledata рисуется тонкой плитой.</summary>
        public const int MinThickness = 1;

        /// <summary>Высота цилиндра моба (около 1,5 м); у сервера высоты моба в клетке нет.</summary>
        public const int MobileHeight = 15;

        /// <summary>Высота кубика предмета на земле.</summary>
        public const int ItemHeight = 4;
    }
}
