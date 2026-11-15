using TheVerge.Core.MapView;
using TheVerge.Core.Movement;
using TheVerge.Core.World;

namespace TheVerge.Presentation.World
{
    /// <summary>Из чего строится отладочная карта одного входа (<see cref="DebugMapPresenter"/>).</summary>
    public readonly struct DebugMapParts
    {
        /// <param name="builder">Блоки карты из файлов данных; null — файлов нет, видны только мобы и предметы.</param>
        /// <param name="inspector">Окно клетки по клику; null — файлов нет.</param>
        /// <param name="others">Шаги показа чужих мобов.</param>
        /// <param name="playerShow">Показ шагов своего персонажа; null — ходьбы нет (без файлов данных), цилиндр — на клетке сервера.</param>
        public DebugMapParts(EntityStore world, SceneBuilder builder, CellInspector inspector, OthersMotion others,
            PlayerStepShow playerShow)
        {
            World = world;
            Builder = builder;
            Inspector = inspector;
            Others = others;
            PlayerShow = playerShow;
        }

        public EntityStore World { get; }

        public SceneBuilder Builder { get; }

        public CellInspector Inspector { get; }

        public OthersMotion Others { get; }

        public PlayerStepShow PlayerShow { get; }
    }
}
