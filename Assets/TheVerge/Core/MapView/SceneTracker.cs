using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Решает, когда пересобирать отладочную карту: свой персонаж перешёл на другую клетку, сменилась карта, мир очищен,
    /// появился, изменился или исчез дом или корабль. Движение чужих мобов и обычные предметы карту не трогают.
    /// Вызывается из главного потока; <see cref="TryTakeRebuild"/> — раз в кадр.
    /// </summary>
    public sealed class SceneTracker : IDisposable
    {
        private readonly EntityStore _world;
        private readonly HashSet<uint> _multis = new HashSet<uint>();
        private bool _dirty = true;
        private bool _built;
        private int _x;
        private int _y;
        private int _map = EntityStore.UnknownMap;
        private bool _disposed;

        public SceneTracker(EntityStore world)
        {
            _world = world;
            _world.ItemChanged += OnItemChanged;
            _world.Removed += OnRemoved;
            _world.Cleared += OnCleared;
        }

        /// <summary>true — пора пересобирать; отдаёт клетку персонажа и номер карты, запоминает их как собранные.</summary>
        public bool TryTakeRebuild(out int centerX, out int centerY, out int map)
        {
            var player = _world.Player;
            centerX = centerY = 0;
            map = _world.MapIndex;
            if (player == null)
            {
                return false;
            }

            centerX = player.Position.X;
            centerY = player.Position.Y;
            if (_built && !_dirty && centerX == _x && centerY == _y && map == _map)
            {
                return false;
            }

            _built = true;
            _dirty = false;
            _x = centerX;
            _y = centerY;
            _map = map;
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _world.ItemChanged -= OnItemChanged;
            _world.Removed -= OnRemoved;
            _world.Cleared -= OnCleared;
        }

        private void OnItemChanged(Item item)
        {
            if (item.IsMulti)
            {
                _multis.Add(item.Serial.Value);
                _dirty = true;
            }
        }

        private void OnRemoved(Serial serial)
        {
            if (_multis.Remove(serial.Value))
            {
                _dirty = true;
            }
        }

        private void OnCleared()
        {
            _multis.Clear();
            _dirty = true;
        }
    }
}
