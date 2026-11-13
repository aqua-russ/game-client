using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Мир клиента — реестр мобов и предметов по серийнику (PLAN Стадия 2, п. 7) со связями «контейнер → содержимое» и
    /// «моб → надетое». Меняется только из обработчиков пакетов сервера; о каждом изменении — событие. Отказ (не тот вид
    /// серийника, неизвестный моб, цикл вложенности) мир не меняет и считается в <see cref="Metrics"/>.
    /// Сервер шлёт объект заново при каждом изменении, поэтому известный объект обновляется на месте без новой памяти.
    /// </summary>
    public sealed class EntityStore
    {
        private readonly Dictionary<uint, Mobile> _mobiles = new Dictionary<uint, Mobile>();
        private readonly Dictionary<uint, Item> _items = new Dictionary<uint, Item>();
        private readonly EntityRemoval _removal;

        public EntityStore()
        {
            _removal = new EntityRemoval(_mobiles, _items, Spatial);
        }

        public event Action<Mobile>? MobileChanged;

        public event Action<Item>? ItemChanged;

        /// <summary>Объект удалён (вместе с ним — всё, что в нём лежало или было надето: о каждом отдельно).</summary>
        public event Action<Serial>? Removed;

        /// <summary>Мир очищен целиком (новый вход, смена карты); отдельных <see cref="Removed"/> нет.</summary>
        public event Action? Cleared;

        public WorldMetrics Metrics { get; } = new WorldMetrics();

        public SpatialIndex Spatial { get; } = new SpatialIndex();

        /// <summary>Номер карты ещё не приходил (0xBF 0x08).</summary>
        public const int UnknownMap = -1;

        public Serial PlayerSerial { get; private set; }

        /// <summary>Карта, на которой мир (0xBF 0x08); <see cref="UnknownMap"/> — ещё не известна.</summary>
        public int MapIndex { get; private set; } = UnknownMap;

        /// <summary>Удалять объекты дальше дальности обзора, когда свой персонаж сдвинулся (умолчание — ClientLimits).</summary>
        public bool PruneOutOfRange { get; set; } = ClientLimits.RemoveOutOfRange;

        /// <summary>Карта (0xBF 0x08): та же или первая — ничего (при входе она приходит дважды); другая — остаётся только свой
        /// персонаж со своими вещами (сервер сам очищает экран и шлёт всё заново: Mobile.Map).</summary>
        public void ChangeMap(int map)
        {
            if (MapIndex != UnknownMap && map != MapIndex)
            {
                Clear(keepPlayer: true);
            }

            MapIndex = map;
        }

        /// <summary>Свой персонаж на новом месте (0x76): остальное о нём не меняется.</summary>
        public void MovePlayerTo(Point3D position)
        {
            var player = Player;
            if (player != null)
            {
                UpdateMobile(player.Serial, new MobileInfo(player.Body, position, player.Direction, player.Hue, player.Flags, player.Notoriety));
            }
        }

        public Mobile? Player => FindMobile(PlayerSerial);

        public int MobileCount => _mobiles.Count;

        public int ItemCount => _items.Count;

        public Mobile? FindMobile(Serial serial) => _mobiles.TryGetValue(serial.Value, out var mobile) ? mobile : null;

        public Item? FindItem(Serial serial) => _items.TryGetValue(serial.Value, out var item) ? item : null;

        /// <summary>Новый вход в мир (0x1B): прежний мир забыт, в новом — свой персонаж.</summary>
        public void Reset(PlayerState player)
        {
            Clear(keepPlayer: false);
            MapIndex = UnknownMap;
            PlayerSerial = player.Serial;
            var info = new MobileInfo(player.Body, player.Position, player.Direction, 0, 0, MobileInfo.NotorietyUnknown);
            UpdateMobile(player.Serial, info);
        }

        /// <summary>Моб появился или изменился (0x78, 0x20, 0xF3).</summary>
        public WorldResult UpdateMobile(Serial serial, in MobileInfo info)
        {
            if (!serial.IsMobile)
            {
                return Reject(WorldResult.InvalidSerial);
            }

            if (_mobiles.TryGetValue(serial.Value, out var mobile))
            {
                var from = mobile.Position;
                mobile.Apply(info);
                Spatial.MoveMobile(mobile, from);
                if (PruneOutOfRange && serial == PlayerSerial && (from.X != info.Position.X || from.Y != info.Position.Y))
                {
                    _removal.RemoveOutOfRange(mobile, Removed);
                }
            }
            else
            {
                mobile = new Mobile(serial);
                _mobiles.Add(serial.Value, mobile);
                mobile.Apply(info);
                Spatial.AddMobile(mobile);
            }

            MobileChanged?.Invoke(mobile);
            return WorldResult.Applied;
        }

        /// <summary>Моб сдвинулся (0x77). Неизвестный моб создаётся заглушкой — без надетого — и считается.</summary>
        public WorldResult MoveMobile(Serial serial, in MobileInfo info)
        {
            if (serial.IsMobile && !_mobiles.ContainsKey(serial.Value))
            {
                Metrics.UnknownMobileMoves++;
            }

            return UpdateMobile(serial, info);
        }

        /// <summary>Предмет лежит на земле (0xF3).</summary>
        public WorldResult PlaceInWorld(Serial serial, in ItemInfo info)
        {
            if (!serial.IsItem)
            {
                return Reject(WorldResult.InvalidSerial);
            }

            var item = TakeItem(serial);
            item.Apply(info);
            item.Place = ItemPlace.World;
            Spatial.AddItem(item);
            ItemChanged?.Invoke(item);
            return WorldResult.Applied;
        }

        /// <summary>
        /// Предмет лежит в контейнере (0x25, 0x3C). Неизвестный контейнер создаётся заглушкой (<see cref="ItemPlace.Unplaced"/>)
        /// и считается; когда он придёт сам, заглушка заполнится.
        /// </summary>
        public WorldResult PlaceInContainer(Serial serial, in ItemInfo info, Serial container, byte gridIndex)
        {
            if (!serial.IsItem || !container.IsItem)
            {
                return Reject(WorldResult.InvalidSerial);
            }

            var parent = FindItem(container);
            var existing = FindItem(serial);
            if (serial == container || (parent != null && existing != null && ItemTree.Contains(existing, parent)))
            {
                return Reject(WorldResult.Cycle);
            }

            parent ??= NewStubContainer(container);
            var item = TakeItem(serial);
            item.Apply(info);
            item.Place = ItemPlace.Container;
            item.GridIndex = gridIndex;
            item.PendingRemoval = false;
            item.Container = parent;
            parent.AddContent(item);
            ItemChanged?.Invoke(item);
            return WorldResult.Applied;
        }

        /// <summary>Предмет надет на моба (0x2E, предметы 0x78). Прежний предмет на этом слое удаляется.</summary>
        public WorldResult Equip(Serial serial, in ItemInfo info, Serial mobileSerial, Layer layer)
        {
            if (!serial.IsItem || !mobileSerial.IsMobile)
            {
                return Reject(WorldResult.InvalidSerial);
            }

            if (!Mobile.IsSlot(layer))
            {
                return Reject(WorldResult.InvalidLayer);
            }

            var mobile = FindMobile(mobileSerial);
            if (mobile == null)
            {
                return Reject(WorldResult.UnknownParent);
            }

            var occupant = mobile.GetEquipped(layer);
            if (occupant != null && occupant.Serial != serial)
            {
                _removal.RemoveItem(occupant, Removed);
            }

            var item = TakeItem(serial);
            item.Apply(info);
            item.Place = ItemPlace.Equipped;
            item.Layer = layer;
            item.Owner = mobile;
            item.PendingRemoval = false;
            mobile.SetSlot(layer, item);
            ItemChanged?.Invoke(item);
            return WorldResult.Applied;
        }

        /// <summary>Начало полного списка надетого (0x78): что не будет надето до <see cref="EndEquipment"/>, снято.</summary>
        public void BeginEquipment(Serial mobileSerial) => FindMobile(mobileSerial)?.MarkEquipmentPending();

        /// <summary>Конец полного списка надетого: не повторённые предметы удаляются (сервер их больше не показывает).</summary>
        public void EndEquipment(Serial mobileSerial)
        {
            var mobile = FindMobile(mobileSerial);
            if (mobile != null)
            {
                _removal.RemovePendingEquipment(mobile, Removed);
            }
        }

        /// <summary>Удалить объект со всем содержимым (0x1D). Неизвестный — ничего не делается, считается.</summary>
        public bool Remove(Serial serial)
        {
            if (serial == PlayerSerial && serial.IsValid)
            {
                Reject(WorldResult.Protected);
                return false;
            }

            if (_mobiles.TryGetValue(serial.Value, out var mobile))
            {
                _removal.RemoveMobile(mobile, Removed);
                return true;
            }

            if (_items.TryGetValue(serial.Value, out var item))
            {
                _removal.RemoveItem(item, Removed);
                return true;
            }

            Metrics.UnknownRemoves++;
            return false;
        }

        /// <summary>Забыть мир. С <paramref name="keepPlayer"/> остаются свой персонаж, его надетое и известное содержимое.</summary>
        public void Clear(bool keepPlayer)
        {
            var player = keepPlayer ? Player : null;
            if (player == null)
            {
                _removal.RemoveAll();
                PlayerSerial = Serial.Zero;
            }
            else
            {
                _removal.RemoveAllExcept(player);
            }

            Cleared?.Invoke();
        }

        private Item NewStubContainer(Serial serial)
        {
            var stub = new Item(serial);
            _items.Add(serial.Value, stub);
            Metrics.StubContainers++;
            return stub;
        }

        /// <summary>Известный предмет, снятый с прежнего места, или новый.</summary>
        private Item TakeItem(Serial serial)
        {
            if (_items.TryGetValue(serial.Value, out var item))
            {
                _removal.Detach(item);
                return item;
            }

            item = new Item(serial);
            _items.Add(serial.Value, item);
            return item;
        }

        private WorldResult Reject(WorldResult result)
        {
            Metrics.Rejected++;
            return result;
        }
    }
}
