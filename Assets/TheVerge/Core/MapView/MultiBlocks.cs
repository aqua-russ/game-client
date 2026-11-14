using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Части домов и кораблей в квадрате обзора: сервер присылает дом одним предметом (0xF3, тип multi), а части берутся из
    /// файла данных со сдвигом от его клетки (MultiData.cs сервера). Свойства каждой части — по её номеру в tiledata.
    /// </summary>
    internal sealed class MultiBlocks
    {
        private readonly ITileData _tiles;
        private readonly IMultiData _multis;
        private readonly EntityStore _world;
        private readonly List<Item> _items = new List<Item>();

        public MultiBlocks(ITileData tiles, IMultiData multis, EntityStore world)
        {
            _tiles = tiles;
            _multis = multis;
            _world = world;
        }

        /// <summary>Блок плитки предмета: верх — Z плюс высота по tiledata, но не тоньше <see cref="SceneLimits.MinThickness"/>.</summary>
        public static SceneBlock Block(SceneKind kind, int x, int y, int z, ItemTileData data)
        {
            var top = z + data.CalcHeight;
            if (top < z + SceneLimits.MinThickness)
            {
                top = z + SceneLimits.MinThickness;
            }

            return new SceneBlock(kind, x, y, z, top, CellClassifier.Classify(data.Flags, false));
        }

        public void Add(int centerX, int centerY, int radius, List<SceneBlock> result)
        {
            _world.Spatial.QueryItems(centerX, centerY, ServerLimits.MultiUpdateRange, _items);
            foreach (var item in _items)
            {
                if (item.IsMulti)
                {
                    AddParts(item, centerX, centerY, radius, result);
                }
            }
        }

        private void AddParts(Item item, int centerX, int centerY, int radius, List<SceneBlock> result)
        {
            var multi = _multis.Get(item.Graphic);
            var parts = multi.Components;
            for (var i = 0; i < parts.Count; i++)
            {
                var x = item.Position.X + parts[i].X;
                var y = item.Position.Y + parts[i].Y;
                if (multi.IsPlaced(i) && System.Math.Abs(x - centerX) <= radius && System.Math.Abs(y - centerY) <= radius)
                {
                    var z = parts[i].WorldZ(item.Position.Z);
                    result.Add(Block(SceneKind.MultiPart, x, y, z, _tiles.Item(parts[i].ItemId)));
                }
            }
        }
    }
}
