using System.Collections.Generic;

namespace TheVerge.Core.Multis
{
    /// <summary>
    /// Дом или корабль из файлов данных: все части и границы, как MultiComponentList сервера (MultiData.cs): границы
    /// считаются по первой части и частям с ненулевыми флагами, начиная с (0, 0); центр — (−MinX, −MinY);
    /// ширина и высота — Max − Min + 1. Неизвестный номер — <see cref="Empty"/> (без частей, размер 0×0).
    /// </summary>
    public sealed class MultiComponents
    {
        public static readonly MultiComponents Empty = new MultiComponents(new MultiComponent[0], 0, 0, 0, 0, true);

        private readonly MultiComponent[] _components;

        private MultiComponents(MultiComponent[] components, int minX, int minY, int maxX, int maxY, bool empty)
        {
            _components = components;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Width = empty ? 0 : maxX - minX + 1;
            Height = empty ? 0 : maxY - minY + 1;
        }

        public IReadOnlyList<MultiComponent> Components => _components;
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }
        public int CenterX => -MinX;
        public int CenterY => -MinY;
        public int Width { get; }
        public int Height { get; }

        public static MultiComponents From(MultiComponent[] components)
        {
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            for (var i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (i != 0 && c.Flags == 0)
                {
                    continue;
                }

                minX = c.X < minX ? c.X : minX;
                minY = c.Y < minY ? c.Y : minY;
                maxX = c.X > maxX ? c.X : maxX;
                maxY = c.Y > maxY ? c.Y : maxY;
            }

            return new MultiComponents(components, minX, minY, maxX, maxY, false);
        }
    }
}
