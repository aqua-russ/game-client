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
    /// персонаж — другого цвета. Множество — по серийнику.
    /// </summary>
    public sealed class EntityMarkers : IDisposable
    {
        private const float MobileRadius = 0.35f;
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

        public EntityMarkers(EntityStore world, Transform root, DebugMaterials materials)
        {
            _world = world;
            _root = root;
            _materials = materials;
            _mobiles = NewPool(PrimitiveType.Cylinder, true);
            _items = NewPool(PrimitiveType.Cube, false);
            _world.MobileChanged += OnMobile;
            _world.ItemChanged += OnItem;
            _world.Removed += OnRemoved;
            _world.Cleared += OnCleared;
            Sync();
        }

        public int Count => _markers.Count;

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
            marker.Renderer.sharedMaterial = mobile.Serial == _world.PlayerSerial ? _materials.Player : _materials.Mobile;
            Place(marker, mobile.Position, SceneLimits.MobileHeight);
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
            // Цилиндр Unity высотой 2 м: половина высоты — масштаб по Y.
            gameObject.transform.localScale = mobile
                ? new Vector3(MobileRadius * 2f, CellSpace.Height(SceneLimits.MobileHeight) / 2f, MobileRadius * 2f)
                : new Vector3(ItemSide, CellSpace.Height(SceneLimits.ItemHeight), ItemSide);
            var renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterial = mobile ? _materials.Mobile : _materials.Item;
            return new Marker { Object = gameObject, Renderer = renderer, IsMobile = mobile };
        }
    }
}
