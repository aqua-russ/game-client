using System;
using System.Collections.Generic;
using TheVerge.Core.MapView;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using UnityEngine;
using UnityEngine.Pool;

namespace TheVerge.Presentation.World
{
    /// <summary>
    /// Мобы — цилиндры, предметы на земле — кубики (PLAN Стадия 2, п. 13). Следует за событиями мира; объекты берутся из
    /// пула и возвращаются в него по удалению (0x1D), очистке мира и уходу предмета в контейнер или на моба. Свой
    /// персонаж — другого цвета; если его ведёт показыватель (<see cref="PlayerShower"/>), здесь он только создаётся, а
    /// двигает его показыватель. Чужие мобы пока встают на клетку сервера (их показыватель — следующая часть задачи 3.9).
    /// Множество — по серийнику.
    /// </summary>
    public sealed class EntityMarkers : IDisposable
    {
        private const float MobileRadius = 0.35f;
        private const float PlayerRadius = 0.5f;
        private const float ItemSide = 0.4f;

        private sealed class Marker
        {
            public GameObject Object;
            public Renderer Renderer;
            public bool IsMobile;
        }

        private readonly EntityStore _world;
        private readonly Transform _root;
        private readonly DebugMaterials _materials;
        private readonly Dictionary<uint, Marker> _markers = new Dictionary<uint, Marker>();
        private readonly ObjectPool<Marker> _mobiles;
        private readonly ObjectPool<Marker> _items;
        private readonly bool _playerShownSeparately;

        /// <param name="playerShownSeparately">Свой цилиндр двигает показыватель шагов, а не события мира.</param>
        public EntityMarkers(EntityStore world, Transform root, DebugMaterials materials, bool playerShownSeparately)
        {
            _world = world;
            _root = root;
            _materials = materials;
            _playerShownSeparately = playerShownSeparately;
            _mobiles = NewPool(PrimitiveType.Cylinder, true);
            _items = NewPool(PrimitiveType.Cube, false);
            _world.MobileChanged += OnMobile;
            _world.ItemChanged += OnItem;
            _world.Removed += OnRemoved;
            _world.Cleared += OnCleared;
            Sync();
        }

        public int Count => _markers.Count;

        /// <summary>Середина цилиндра моба, стоящего на точке земли <paramref name="ground"/> (клетка → сцена — CellSpace).</summary>
        public static Vector3 MobileCenter(Vector3 ground) =>
            new Vector3(ground.x, ground.y + CellSpace.Height(SceneLimits.MobileHeight) / 2f, ground.z);

        /// <summary>Цилиндр моба, если он есть на карте.</summary>
        public bool TryGetMobile(Serial serial, out Transform marker)
        {
            if (_markers.TryGetValue(serial.Value, out var found) && found.IsMobile)
            {
                marker = found.Object.transform;
                return true;
            }

            marker = null;
            return false;
        }

        public void Dispose()
        {
            _world.MobileChanged -= OnMobile;
            _world.ItemChanged -= OnItem;
            _world.Removed -= OnRemoved;
            _world.Cleared -= OnCleared;
            ReleaseAll();
            _mobiles.Dispose();
            _items.Dispose();
        }

        // Всё, что уже есть в мире (вход мог закончиться до создания карты) и что осталось после смены карты.
        private void Sync()
        {
            foreach (var mobile in _world.Mobiles)
            {
                OnMobile(mobile);
            }

            foreach (var item in _world.Items)
            {
                OnItem(item);
            }
        }

        private void OnMobile(Mobile mobile)
        {
            var marker = Take(mobile.Serial, true);
            var isPlayer = mobile.Serial == _world.PlayerSerial;
            marker.Renderer.sharedMaterial = isPlayer ? _materials.Player : _materials.Mobile;
            // Свой цилиндр шире остальных: если на клетке стоит ещё кто-то, голубой виден вокруг него.
            marker.Object.transform.localScale = MobileScale(isPlayer ? PlayerRadius : MobileRadius);
            if (!isPlayer || !_playerShownSeparately)
            {
                Place(marker, mobile.Position, SceneLimits.MobileHeight);
            }
        }

        private void OnItem(Item item)
        {
            if (item.Place != ItemPlace.World || item.IsMulti)
            {
                Release(item.Serial.Value);
                return;
            }

            Place(Take(item.Serial, false), item.Position, SceneLimits.ItemHeight);
        }

        private void OnRemoved(Serial serial) => Release(serial.Value);

        private void OnCleared()
        {
            ReleaseAll();
            Sync();
        }

        // Цилиндр Unity высотой 2 м: половина высоты — масштаб по Y.
        private static Vector3 MobileScale(float radius) =>
            new Vector3(radius * 2f, CellSpace.Height(SceneLimits.MobileHeight) / 2f, radius * 2f);

        private static void Place(Marker marker, Point3D position, int heightZ)
        {
            var center = CellSpace.CellCenter(position.X, position.Y, position.Z);
            center.y += CellSpace.Height(heightZ) / 2f;
            marker.Object.transform.position = center;
        }

        private Marker Take(Serial serial, bool mobile)
        {
            if (_markers.TryGetValue(serial.Value, out var marker) && marker.IsMobile == mobile)
            {
                return marker;
            }

            Release(serial.Value);
            marker = (mobile ? _mobiles : _items).Get();
            _markers[serial.Value] = marker;
            return marker;
        }

        private void Release(uint serial)
        {
            if (_markers.TryGetValue(serial, out var marker))
            {
                _markers.Remove(serial);
                (marker.IsMobile ? _mobiles : _items).Release(marker);
            }
        }

        private void ReleaseAll()
        {
            foreach (var marker in _markers.Values)
            {
                (marker.IsMobile ? _mobiles : _items).Release(marker);
            }

            _markers.Clear();
        }

        private ObjectPool<Marker> NewPool(PrimitiveType shape, bool mobile) => new ObjectPool<Marker>(
            () => Create(shape, mobile),
            marker => marker.Object.SetActive(true),
            marker => marker.Object.SetActive(false),
            marker => UnityEngine.Object.Destroy(marker.Object));

        private Marker Create(PrimitiveType shape, bool mobile)
        {
            var gameObject = GameObject.CreatePrimitive(shape);
            UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
            gameObject.name = mobile ? "Mobile" : "Item";
            gameObject.transform.SetParent(_root, false);
            gameObject.transform.localScale = mobile
                ? MobileScale(MobileRadius)
                : new Vector3(ItemSide, CellSpace.Height(SceneLimits.ItemHeight), ItemSide);
            var renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterial = mobile ? _materials.Mobile : _materials.Item;
            return new Marker { Object = gameObject, Renderer = renderer, IsMobile = mobile };
        }
    }
}
