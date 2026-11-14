using System;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;

namespace TheVerge.Core.Movement
{
    /// <summary>
    /// Выбор опоры и проверка препятствий клетки по правилам ModernUO (Engines/Pathing/Movement.cs: GetStartZ, Check,
    /// IsOk). Работает на переиспользуемых буферах <see cref="MovementCellData"/> без выделения памяти.
    /// </summary>
    internal sealed class MovementSurface
    {
        private const int PersonHeight = 16;
        private const int StepHeight = 2;

        private readonly MovementCellData _cell;
        private int _startZ;
        private int _startCenter;
        private int _startTop;
        private bool _startSet;
        private int _bestZ;
        private bool _moveIsOk;

        public MovementSurface(IMapData map, ITileData tileData, IMultiData? multis, EntityStore? entities)
        {
            _cell = new MovementCellData(map, tileData, multis, entities);
        }

        public int StartZ => _startZ;

        public void ComputeStart(Point3D loc, bool ignoreMovableImpassables)
        {
            _cell.Collect(loc.X, loc.Y, ignoreMovableImpassables);
            _startZ = 0;
            _startCenter = 0;
            _startTop = 0;
            _startSet = false;
            if (_cell.LandConsider && !_cell.LandBlocks && loc.Z >= _cell.LandCenter)
            {
                _startZ = _cell.LandZ;
                _startCenter = _cell.LandCenter;
                _startTop = _cell.LandTop;
                _startSet = true;
            }

            UpdateStartFromTiles(loc.Z);
            UpdateStartFromItems(loc.Z);
            if (!_startSet)
            {
                _startZ = loc.Z;
                _startTop = loc.Z;
            }
            else if (loc.Z > _startTop)
            {
                _startTop = loc.Z;
            }
        }

        public bool CheckCell(int x, int y, int locZ, MoverState mover, out int newZ)
        {
            _cell.Collect(x, y, mover.IgnoreMovableImpassables);
            int stepTop = _startTop + StepHeight;
            int checkTop = _startZ + PersonHeight;
            _bestZ = 0;
            _moveIsOk = false;

            TryTileSurfaces(locZ, stepTop, checkTop, mover.IgnoreDoors);
            TryItemSurfaces(locZ, stepTop, checkTop, mover.IgnoreDoors);
            TryLandSurface(locZ, stepTop, checkTop, mover.IgnoreDoors);

            newZ = _moveIsOk ? _bestZ : _startZ;
            return _moveIsOk;
        }

        private void UpdateStartFromTiles(int locZ)
        {
            var tiles = _cell.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var data = _cell.TileItem(tile.Id);
                if (data.Has(TileFlags.Surface))
                {
                    ConsiderStartSurface(tile.Z, data, locZ);
                }
            }
        }

        private void UpdateStartFromItems(int locZ)
        {
            var items = _cell.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var data = _cell.TileItem(item.Graphic);
                if (data.Has(TileFlags.Surface))
                {
                    ConsiderStartSurface(item.Position.Z, data, locZ);
                }
            }
        }

        private void ConsiderStartSurface(int baseZ, in ItemTileData data, int locZ)
        {
            int calcTop = baseZ + data.CalcHeight;
            if ((_startSet && calcTop < _startCenter) || locZ < calcTop)
            {
                return;
            }

            _startZ = baseZ;
            _startCenter = calcTop;
            int top = baseZ + data.Height;
            if (!_startSet || top > _startTop)
            {
                _startTop = top;
            }

            _startSet = true;
        }

        private void TryTileSurfaces(int locZ, int stepTop, int checkTop, bool ignoreDoors)
        {
            var tiles = _cell.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var data = _cell.TileItem(tile.Id);
                if ((data.Flags & MovementCellData.ImpassableSurface) == TileFlags.Surface)
                {
                    TryItemOrTileSurface(tile.Z, data, locZ, stepTop, checkTop, ignoreDoors);
                }
            }
        }

        private void TryItemSurfaces(int locZ, int stepTop, int checkTop, bool ignoreDoors)
        {
            var items = _cell.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.IsMovable)
                {
                    continue;
                }

                var data = _cell.TileItem(item.Graphic);
                if ((data.Flags & MovementCellData.ImpassableSurface) == TileFlags.Surface)
                {
                    TryItemOrTileSurface(item.Position.Z, data, locZ, stepTop, checkTop, ignoreDoors);
                }
            }
        }

        private void TryItemOrTileSurface(int itemZ, in ItemTileData data, int locZ, int stepTop, int checkTop, bool ignoreDoors)
        {
            int ourZ = itemZ + data.CalcHeight;
            if (!IsCloserCandidate(ourZ, locZ))
            {
                return;
            }

            int itemTop = data.Has(TileFlags.Bridge) ? itemZ : itemZ + data.Height;
            if (stepTop < itemTop)
            {
                return;
            }

            int testTop = Math.Max(checkTop, ourZ + PersonHeight);
            int landCheck = itemZ + Math.Min(data.Height, StepHeight);
            if (_cell.LandConsider && landCheck < _cell.LandCenter && _cell.LandCenter > ourZ && testTop > _cell.LandZ)
            {
                return;
            }

            if (IsOk(ignoreDoors, ourZ, testTop))
            {
                _bestZ = ourZ;
                _moveIsOk = true;
            }
        }

        private void TryLandSurface(int locZ, int stepTop, int checkTop, bool ignoreDoors)
        {
            if (!_cell.LandConsider || _cell.LandBlocks || stepTop < _cell.LandZ)
            {
                return;
            }

            int landCenter = _cell.LandCenter;
            if (!IsCloserCandidate(landCenter, locZ))
            {
                return;
            }

            int testTop = Math.Max(checkTop, landCenter + PersonHeight);
            if (IsOk(ignoreDoors, landCenter, testTop))
            {
                _bestZ = landCenter;
                _moveIsOk = true;
            }
        }

        private bool IsCloserCandidate(int ourZ, int locZ)
        {
            if (!_moveIsOk)
            {
                return true;
            }

            int cmp = Math.Abs(ourZ - locZ) - Math.Abs(_bestZ - locZ);
            return cmp < 0 || (cmp == 0 && ourZ <= _bestZ);
        }

        private bool IsOk(bool ignoreDoors, int ourZ, int ourTop)
        {
            var tiles = _cell.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (BlocksSpan(tile.Id, tile.Z, _cell.TileItem(tile.Id), false, ourZ, ourTop))
                {
                    return false;
                }
            }

            var items = _cell.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (BlocksSpan(item.Graphic, item.Position.Z, _cell.TileItem(item.Graphic), ignoreDoors, ourZ, ourTop))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BlocksSpan(int id, int z, in ItemTileData data, bool ignoreDoors, int ourZ, int ourTop)
        {
            if ((data.Flags & MovementCellData.ImpassableSurface) == 0 || (ignoreDoors && IsDoor(id, data)))
            {
                return false;
            }

            return z + data.CalcHeight > ourZ && ourTop > z;
        }

        private static bool IsDoor(int id, in ItemTileData data) =>
            data.Has(TileFlags.Door) || id == 0x692 || id == 0x846 || id == 0x873 || (id >= 0x6F5 && id <= 0x6F6);
    }
}
