using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Моб в мире клиента: последнее, что о нём сказал сервер, и надетое по слоям (на слое — один предмет, как
    /// Mobile.EquipItem сервера). Меняет его только <see cref="EntityStore"/>.
    /// </summary>
    public sealed class Mobile
    {
        /// <summary>Слотов экипировки: слои 0x01–0x1D (Layer.cs сервера), 0 не используется.</summary>
        internal const int SlotCount = (int)Layer.Bank + 1;

        private readonly Item?[] _slots = new Item?[SlotCount];

        internal Mobile(Serial serial)
        {
            Serial = serial;
        }

        public Serial Serial { get; }

        public ushort Body { get; private set; }

        public Point3D Position { get; private set; }

        public Direction Direction { get; private set; }

        public ushort Hue { get; private set; }

        public byte Flags { get; private set; }

        public byte Notoriety { get; private set; }

        public int EquippedCount { get; private set; }

        /// <summary>Здоровье, статы и полоски по пакетам состояния (PLAN Стадия 4, п. 4).</summary>
        public MobileStatus Status { get; } = new MobileStatus();

        /// <summary>Что надето на слое; null — ничего или слой вне Layer.</summary>
        public Item? GetEquipped(Layer layer) => IsSlot(layer) ? _slots[(int)layer] : null;

        public override string ToString() => "моб " + Serial + " тело 0x" + Body.ToString("X4") + " " + Position;

        internal static bool IsSlot(Layer layer) => layer > Layer.Invalid && (int)layer < SlotCount;

        internal void Apply(in MobileInfo info)
        {
            Body = info.Body;
            Position = info.Position;
            Direction = info.Direction;
            Hue = info.Hue;
            Flags = info.Flags;
            if (info.Notoriety != MobileInfo.NotorietyUnknown)
            {
                Notoriety = info.Notoriety;
            }
        }

        /// <summary>Отметить всё надетое к снятию: что не будет надето снова до конца списка 0x78, снимется.</summary>
        internal void MarkEquipmentPending()
        {
            foreach (var item in _slots)
            {
                if (item != null)
                {
                    item.PendingRemoval = true;
                }
            }
        }

        internal void SetSlot(Layer layer, Item? item)
        {
            var index = (int)layer;
            if (_slots[index] == null && item != null)
            {
                EquippedCount++;
            }
            else if (_slots[index] != null && item == null)
            {
                EquippedCount--;
            }

            _slots[index] = item;
        }
    }
}
