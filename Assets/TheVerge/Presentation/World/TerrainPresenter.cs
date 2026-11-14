using System;
using System.Collections.Generic;
using TheVerge.Core.MapView;
using TheVerge.Core.World;
using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Ведёт карту вокруг персонажа: когда <see cref="SceneTracker"/> просит, собирает блоки (<see cref="SceneBuilder"/>) и
    /// кладёт их в сетки. Без данных карты или на карте, файлов которой нет, показывает только пояснение в
    /// <see cref="Status"/>. Вызывается раз в кадр.
    /// </summary>
    public sealed class TerrainPresenter : IDisposable
    {
        private readonly SceneTracker _tracker;
        private readonly SceneBuilder _builder;
        private readonly TerrainMeshes _meshes;
        private readonly List<SceneBlock> _blocks = new List<SceneBlock>();

        /// <param name="builder">null — файлы данных не загружены.</param>
        public TerrainPresenter(EntityStore world, SceneBuilder builder, TerrainMeshes meshes)
        {
            _tracker = new SceneTracker(world);
            _builder = builder;
            _meshes = meshes;
            Status = builder == null ? "Карта: файлы данных не загружены" : string.Empty;
        }

        /// <summary>Строка для панели «в мире»: что показано или почему карты нет.</summary>
        public string Status { get; private set; }

        /// <summary>Блоки карты, которые сейчас нарисованы (для выбора клетки кликом).</summary>
        public IReadOnlyList<SceneBlock> Blocks => _blocks;

        public void Update()
        {
            if (!_tracker.TryTakeRebuild(out var x, out var y, out var map) || _builder == null)
            {
                return;
            }

            if (map != 0 && map != EntityStore.UnknownMap)
            {
                _blocks.Clear();
                _meshes.Clear();
                Status = "Карта " + map + ": данных нет (есть только Фелукка)";
                return;
            }

            _builder.Build(x, y, SceneViewRadius, _blocks);
            _meshes.Rebuild(_blocks);
            Status = "Карта: блоков " + _blocks.Count + ", центр (" + x + ", " + y + ")";
        }

        public void Dispose() => _tracker.Dispose();

        private static int SceneViewRadius => TheVerge.Core.Session.ServerLimits.UpdateRange;
    }
}
