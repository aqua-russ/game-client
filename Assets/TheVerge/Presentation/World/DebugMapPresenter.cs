using System;
using TheVerge.Core.MapView;
using TheVerge.Core.World;
using TheVerge.Presentation.Camera;
using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Отладочная 3D-карта вокруг персонажа (PLAN Стадия 2, п. 13): земля и статика блоками, мобы и предметы фигурами,
    /// камера сверху. Создаётся при входе в мир, закрывается при выходе. Правил игры здесь нет — только показ мира клиента.
    /// </summary>
    public sealed class DebugMapPresenter : IDisposable
    {
        private readonly EntityStore _world;
        private readonly GameObject _root;
        private readonly DebugMaterials _materials;
        private readonly TerrainPresenter _terrain;
        private readonly EntityMarkers _markers;
        private readonly DebugCamera _camera;

        /// <param name="builder">Блоки карты из файлов данных; null — файлов нет, видны только мобы и предметы.</param>
        public DebugMapPresenter(EntityStore world, SceneBuilder builder, UnityEngine.Camera camera)
        {
            _world = world;
            _root = new GameObject("DebugMap");
            _materials = new DebugMaterials();
            _terrain = new TerrainPresenter(world, builder, new TerrainMeshes(_root.transform, _materials));
            _markers = new EntityMarkers(world, _root.transform, _materials);
            _camera = new DebugCamera(camera);
        }

        public EntityStore Entities => _world;

        /// <summary>Строка для панели «в мире»: что нарисовано или почему карты нет.</summary>
        public string Status => _terrain.Status;

        public void Update()
        {
            _terrain.Update();
            var player = _world.Player;
            if (player != null)
            {
                _camera.Update(CellSpace.CellCenter(player.Position.X, player.Position.Y, player.Position.Z));
            }
        }

        public void Dispose()
        {
            _markers.Dispose();
            _terrain.Dispose();
            _materials.Dispose();
            UnityEngine.Object.Destroy(_root);
        }
    }
}
