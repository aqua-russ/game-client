using System;
using System.Collections.Generic;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>
    /// Предмет в мире клиента: на земле, в контейнере, надетый на моба или заглушка (известен только как чей-то
    /// контейнер). Меняет его только <see cref="EntityStore"/>.
    /// </summary>
    public sealed class Item
    {
        private const byte MovableFlag = 0x20;

        private List<Item>? _contents;

        internal Item(Serial serial)
        {
            Serial = serial;
        }

        public Serial Serial { get; }

        public ushort Graphic { get; private set; }

        public ushort Amount { get; private set; }

        /// <summary>На земле — клетка; в контейнере — точка в окне контейнера (Z = 0); надетый — не используется.</summary>
        public Point3D Position { get; private set; }

        public ushort Hue { get; private set; }

        /// <summary>Флаги Item.GetPacketFlags сервера: 0x20 — переносимый (или украшение с ForceShowProperties), 0x80 — невидимый.</summary>
        public byte Flags { get; private set; }

        /// <summary>Переносимый: такой предмет не служит опорой при шаге, а призрак проходит его насквозь (Movement.cs).</summary>
        public bool IsMovable => (Flags & MovableFlag) != 0;

        public byte Light { get; private set; }

        public bool IsMulti { get; private set; }

        public ItemPlace Place { get; internal set; }

        /// <summary>Номер клетки сетки в окне контейнера (0x3C, 0x25).</summary>
        public byte GridIndex { get; internal set; }

        /// <summary>Слой, если предмет надет.</summary>
        public Layer Layer { get; internal set; }

        /// <summary>Контейнер, в котором лежит предмет.</summary>
        public Item? Container { get; internal set; }

        /// <summary>Моб, на котором надет предмет.</summary>
        public Mobile? Owner { get; internal set; }

        public IReadOnlyList<Item> Contents => (IReadOnlyList<Item>?)_contents ?? Array.Empty<Item>();

        /// <summary>Отмечен к удалению при полном списке (надетое 0x78, содержимое 0x3C): кого список не повторил, тот удалён.</summary>
        internal bool PendingRemoval { get; set; }

        public override string ToString() => "предмет " + Serial + " 0x" + Graphic.ToString("X4") + " " + Place;

        internal List<Item>? ContentsList => _contents;

        internal void Apply(in ItemInfo info)
        {
            Graphic = info.Graphic;
            Amount = info.Amount;
            Position = info.Position;
            Hue = info.Hue;
            Flags = info.Flags;
            Light = info.Light;
            IsMulti = info.IsMulti;
        }

        internal void AddContent(Item item) => (_contents ??= new List<Item>()).Add(item);

        internal void RemoveContent(Item item) => _contents?.Remove(item);
    }
}
