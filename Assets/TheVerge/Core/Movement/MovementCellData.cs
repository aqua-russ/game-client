using System;
using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Сбор земли, статики, частей multi (кроме скрытых частей дизайна без флагов, MultiData.cs) и динамических
    /// предметов клетки в предвыделенные списки. Только из главного потока.
    /// </summary>
    internal sealed class MovementCellData
    {
        public const byte MovableItemFlag = 0x20;
        public const TileFlags ImpassableSurface = TileFlags.Impassable | TileFlags.Surface;

        private readonly IMapData _map;
        private readonly ITileData _tileData;
        private readonly IMultiData? _multis;
        private readonly EntityStore? _entities;
        private readonly List<Item> _multiItems = new List<Item>(32);

        public MovementCellData(IMapData map, ITileData tileData, IMultiData? multis, EntityStore? entities)
        {
            _map = map;
            _tileData = tileData;
            _multis = multis;
            _entities = entities;
        }

        public List<StaticTile> Tiles { get; } = new List<StaticTile>(32);
        public List<Item> Items { get; } = new List<Item>(32);
        public bool LandConsider { get; private set; }
        public bool LandBlocks { get; private set; }
        public int LandCenter { get; private set; }
        public int LandZ { get; private set; }
        public int LandTop { get; private set; }

        public ItemTileData TileItem(int id) => _tileData.Item(id);

        public void Collect(int x, int y, bool ignoreMovableImpassables)
        {
            _map.TryGetStatics(x, y, Tiles);
            if (_entities != null && _multis != null)
            {
                CollectMultiTiles(x, y);
            }

            Items.Clear();
            if (_entities != null)
            {
                CollectGroundItems(x, y, ignoreMovableImpassables);
            }

            ReadLand(x, y);
        }

        private void CollectMultiTiles(int x, int y)
        {
            _entities!.Spatial.QueryItems(x, y, ServerLimits.MultiUpdateRange, _multiItems);
            for (int i = 0; i < _multiItems.Count; i++)
            {
                var item = _multiItems[i];
                if (item.IsMulti)
                {
                    AppendMultiParts(item, x, y);
                }
            }
        }

        private void AppendMultiParts(Item item, int x, int y)
        {
            var parts = _multis!.Get(item.Graphic).Components;
            for (int j = 0; j < parts.Count; j++)
            {
                var part = parts[j];
                if ((j != 0 && part.Flags == TileFlags.None) || item.Position.X + part.X != x || item.Position.Y + part.Y != y)
                {
                    continue;
                }

                int worldZ = (sbyte)((sbyte)part.Z + item.Position.Z);
                Tiles.Add(new StaticTile(part.ItemId, worldZ, 0));
            }
        }

        private void CollectGroundItems(int x, int y, bool ignoreMovableImpassables)
        {
            _entities!.Spatial.QueryItems(x, y, 0, Items);
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (item.IsMulti || item.Place != ItemPlace.World)
                {
                    Items.RemoveAt(i);
                    continue;
                }

                var data = _tileData.Item(item.Graphic);
                bool hasFlag = (data.Flags & ImpassableSurface) != 0;
                bool ignored = ignoreMovableImpassables && (item.Flags & MovableItemFlag) != 0 && hasFlag;
                if (!hasFlag || ignored)
                {
                    Items.RemoveAt(i);
                }
            }
        }

        private void ReadLand(int x, int y)
        {
            _map.TryGetLand(x, y, out var tile);
            LandConsider = !(tile.Id == 2 || tile.Id == 0x1DB || (tile.Id >= 0x1AE && tile.Id <= 0x1B5));
            var data = _tileData.Land(tile.Id);
            LandBlocks = data.Has(TileFlags.Impassable);

            int zTop = tile.Z;
            int zLeft = _map.TryGetLand(x, y + 1, out var leftTile) ? leftTile.Z : 0;
            int zRight = _map.TryGetLand(x + 1, y, out var rightTile) ? rightTile.Z : 0;
            int zBottom = _map.TryGetLand(x + 1, y + 1, out var bottomTile) ? bottomTile.Z : 0;

            LandZ = Math.Min(Math.Min(zTop, zLeft), Math.Min(zRight, zBottom));
            LandTop = Math.Max(Math.Max(zTop, zLeft), Math.Max(zRight, zBottom));
            LandCenter = Math.Abs(zTop - zBottom) > Math.Abs(zLeft - zRight) ? FloorAverage(zLeft, zRight) : FloorAverage(zTop, zBottom);
        }

        private static int FloorAverage(int a, int b)
        {
            int v = a + b;
            if (v < 0)
            {
                --v;
            }

            return v / 2;
        }
    }
}
