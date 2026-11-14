using System.Collections.Generic;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Окно клетки отладочной карты (PLAN Стадия 2, п. 13): всё, что клиент знает о клетке, из тех же данных, что у
    /// <see cref="SceneBuilder"/> и сервера. Порядок: земля, статика (в порядке файла), части домов, предметы на земле,
    /// мобы. Данные карты есть только для Фелукки (карта 0), как и у <see cref="SceneBuilder"/>.
    /// </summary>
    public sealed class CellInspector
    {
        private readonly IMapData _map;
        private readonly ITileData _tiles;
        private readonly IMultiData _multis;
        private readonly EntityStore _world;
        private readonly List<StaticTile> _statics = new List<StaticTile>();
        private readonly List<Item> _items = new List<Item>();
        private readonly List<Mobile> _mobiles = new List<Mobile>();
        private readonly List<CellObject> _objects = new List<CellObject>();

        public CellInspector(IMapData map, ITileData tiles, IMultiData multis, EntityStore world)
        {
            _map = map;
            _tiles = tiles;
            _multis = multis;
            _world = world;
        }

        /// <summary>Объекты клетки в <paramref name="result"/> (очищается); false — клетка вне карты.</summary>
        public bool TryCollect(int x, int y, List<CellObject> result)
        {
            result.Clear();
            if (!_map.TryGetLand(x, y, out var land))
            {
                return false;
            }

            var landData = _tiles.Land(land.Id);
            result.Add(new CellObject(SceneKind.Land, land.Id, land.Z, null, landData.Flags, landData.Name, CellEntity.None));
            AddStatics(x, y, result);
            _world.Spatial.QueryItems(x, y, ServerLimits.MultiUpdateRange, _items);
            AddHouseParts(x, y, result);
            AddGroundItems(x, y, result);
            AddMobiles(x, y, result);
            return true;
        }

        /// <summary>Текст для окна: заголовок и по строке на объект; для клетки вне карты и чужих карт — пояснение.</summary>
        public string Describe(int x, int y)
        {
            var map = _world.MapIndex;
            if (map != EntityStore.UnknownMap && map != 0)
            {
                return CellReport.OtherMap(x, y, map);
            }

            if (!TryCollect(x, y, _objects))
            {
                return CellReport.OutsideMap(x, y, _map.Width, _map.Height);
            }

            return CellReport.Describe(x, y, map, _objects);
        }

        private void AddStatics(int x, int y, List<CellObject> result)
        {
            _map.TryGetStatics(x, y, _statics);
            foreach (var tile in _statics)
            {
                var data = _tiles.Item(tile.Id);
                result.Add(new CellObject(SceneKind.Static, tile.Id, tile.Z, data.CalcHeight, data.Flags, data.Name, new CellEntity(0, tile.Hue, 0)));
            }
        }

        private void AddHouseParts(int x, int y, List<CellObject> result)
        {
            foreach (var item in _items)
            {
                if (!item.IsMulti)
                {
                    continue;
                }

                var multi = _multis.Get(item.Graphic);
                var parts = multi.Components;
                for (var i = 0; i < parts.Count; i++)
                {
                    if (multi.IsPlaced(i) && item.Position.X + parts[i].X == x && item.Position.Y + parts[i].Y == y)
                    {
                        var data = _tiles.Item(parts[i].ItemId);
                        result.Add(new CellObject(SceneKind.MultiPart, parts[i].ItemId, parts[i].WorldZ(item.Position.Z),
                            data.CalcHeight, data.Flags, data.Name, new CellEntity(item.Serial.Value, 0, 0)));
                    }
                }
            }
        }

        private void AddGroundItems(int x, int y, List<CellObject> result)
        {
            foreach (var item in _items)
            {
                if (!item.IsMulti && item.Position.X == x && item.Position.Y == y)
                {
                    var data = _tiles.Item(item.Graphic);
                    result.Add(new CellObject(SceneKind.Item, item.Graphic, item.Position.Z, data.CalcHeight, data.Flags,
                        data.Name, new CellEntity(item.Serial.Value, item.Hue, item.Amount)));
                }
            }
        }

        private void AddMobiles(int x, int y, List<CellObject> result)
        {
            _world.Spatial.QueryMobiles(x, y, 0, _mobiles);
            foreach (var mobile in _mobiles)
            {
                result.Add(new CellObject(SceneKind.Mobile, mobile.Body, mobile.Position.Z, null, TileFlags.None, string.Empty,
                    new CellEntity(mobile.Serial.Value, mobile.Hue, 0)));
            }
        }
    }
}
