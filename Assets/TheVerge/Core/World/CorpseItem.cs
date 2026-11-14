using System;
using TheVerge.Core.Primitives;

namespace TheVerge.Core.World
{
    /// <summary>Вещь на трупе: в каком слое она была надета и её серийник (0x89, PLAN Стадия 4, п. 9).</summary>
    public readonly struct CorpseItem : IEquatable<CorpseItem>
    {
        public CorpseItem(Layer layer, Serial item)
        {
            Layer = layer;
            Item = item;
        }

        public Layer Layer { get; }

        public Serial Item { get; }

        public bool Equals(CorpseItem other) => Layer == other.Layer && Item == other.Item;

        public override bool Equals(object? obj) => obj is CorpseItem other && Equals(other);

        public override int GetHashCode() => ((int)Layer * 397) ^ Item.GetHashCode();

        public override string ToString() => Layer + " → " + Item;
    }
}
