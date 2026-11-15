using System;
using TheVerge.Core.Movement;
using TheVerge.Core.World;
using TheVerge.Presentation.Camera;
using TheVerge.Presentation.UI;
using UnityEngine;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Отладочная 3D-карта вокруг персонажа (PLAN Стадия 2, п. 13; Стадия 3, п. 9): земля и статика блоками, мобы и
    /// предметы фигурами, камера сверху. Свой цилиндр ведёт показыватель шагов (<see cref="PlayerShower"/>), камера следует
    /// за ним плавно; без ходьбы — за клеткой сервера. Создаётся при входе в мир, закрывается при выходе. Правил игры здесь
    /// нет — только показ мира клиента.
    /// </summary>
    public sealed class DebugMapPresenter : IDisposable
    {
        private readonly EntityStore _world;
        private readonly GameObject _root;
        private readonly DebugMaterials _materials;
        private readonly TerrainPresenter _terrain;
        private readonly EntityMarkers _markers;
        private readonly DebugCamera _camera;
        private readonly CellPicking _picking;
        private readonly PlayerShower _player;

        public DebugMapPresenter(DebugMapParts parts, UnityEngine.Camera camera, WorldUiHooks ui)
        {
            _world = parts.World;
            _root = new GameObject("DebugMap");
            _materials = new DebugMaterials();
            _terrain = new TerrainPresenter(_world, parts.Builder, new TerrainMeshes(_root.transform, _materials));
            _markers = new EntityMarkers(_world, _root.transform, _materials, parts.PlayerShow != null);
            _camera = new DebugCamera(camera);
            _picking = new CellPicking(_world, parts.Inspector, camera, ui.PointerOverUi, ui.ShowReport);
            _player = parts.PlayerShow == null ? null : new PlayerShower(parts.PlayerShow);
        }

        public EntityStore Entities => _world;

        /// <summary>Строка для панели «в мире»: что нарисовано или почему карты нет.</summary>
        public string Status => _terrain.Status;

        /// <summary>Раскладка клавиш движения по этой камере.</summary>
        public CameraLayout CameraLayout => _camera.Layout;

        /// <param name="now">Часы ядра (IClock): по ним идёт шаг показа.</param>
        public void Update(long now)
        {
            _terrain.Update();
            _picking.Update(_terrain.Blocks);
            if (_player != null && _markers.TryGetMobile(_world.PlayerSerial, out var marker) && _player.Show(marker, now))
            {
                _camera.Update(_player.Ground);
                return;
            }

            var player = _world.Player;
            if (player != null)
            {
                _camera.Update(CellSpace.CellCenter(player.Position.X, player.Position.Y, player.Position.Z));
            }
        }

        public void Dispose()
        {
            _player?.Dispose();
            _markers.Dispose();
            _terrain.Dispose();
            _materials.Dispose();
            UnityEngine.Object.Destroy(_root);
        }
    }
}
