using System.Collections.Generic;
using TheVerge.Core.Multis;
using TheVerge.Core.Ports;
using TheVerge.Core.Tiles;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>Дома и корабли из памяти.</summary>
    internal sealed class FakeMultis : IMultiData
    {
        private readonly Dictionary<int, MultiComponents> _multis = new Dictionary<int, MultiComponents>();

        public int Count => _multis.Count;

        public void Set(int multiId, params MultiComponent[] parts) => _multis[multiId] = MultiComponents.From(parts);

        public MultiComponents Get(int multiId) => _multis.TryGetValue(multiId & 0x3FFF, out var parts) ? parts : MultiComponents.Empty;
    }
}
