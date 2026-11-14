using System;
using System.Collections.Generic;

namespace TheVerge.Core.MapView
{
    /// <summary>
    /// Какую клетку выбрал луч: пересечение с блоками карты (земля, статика, части домов) и с блоками мобов и предметов
    /// (<see cref="SceneMarkers"/>); побеждает ближайший к началу луча, при равенстве — тот, что позже в списке. Блок —
    /// клетка целиком (x…x+1, y…y+1) от <see cref="SceneBlock.ZBottom"/> до <see cref="SceneBlock.ZTop"/>.
    /// </summary>
    public static class CellPicker
    {
        private const double Epsilon = 1e-12;

        public static bool TryPick(IReadOnlyList<SceneBlock> blocks, in PickRay ray, out PickHit hit)
        {
            hit = default;
            if (Math.Abs(ray.DirX) < Epsilon && Math.Abs(ray.DirY) < Epsilon && Math.Abs(ray.DirZ) < Epsilon)
            {
                return false;
            }

            var found = false;
            var best = double.MaxValue;
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (TryEnter(block, ray, out var distance) && distance <= best)
                {
                    best = distance;
                    hit = new PickHit(block.X, block.Y, block.Kind, distance);
                    found = true;
                }
            }

            return found;
        }

        // Метод плит: пересечение трёх отрезков «луч внутри слоя по оси»; вход — наибольший из входов, не раньше начала луча.
        private static bool TryEnter(in SceneBlock block, in PickRay ray, out double distance)
        {
            distance = 0;
            var enter = 0.0;
            var exit = double.MaxValue;
            return Slab(ray.OriginX, ray.DirX, block.X, block.X + 1, ref enter, ref exit) &&
                   Slab(ray.OriginY, ray.DirY, block.Y, block.Y + 1, ref enter, ref exit) &&
                   Slab(ray.OriginZ, ray.DirZ, block.ZBottom, block.ZTop, ref enter, ref exit) &&
                   Done(enter, exit, out distance);
        }

        private static bool Done(double enter, double exit, out double distance)
        {
            distance = enter;
            return enter <= exit;
        }

        private static bool Slab(double origin, double dir, double min, double max, ref double enter, ref double exit)
        {
            if (Math.Abs(dir) < Epsilon)
            {
                return origin >= min && origin <= max;
            }

            var t1 = (min - origin) / dir;
            var t2 = (max - origin) / dir;
            enter = Math.Max(enter, Math.Min(t1, t2));
            exit = Math.Min(exit, Math.Max(t1, t2));
            return true;
        }
    }
}
