using System.Collections.Generic;
using System.Text;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;

namespace TheVerge.Core.MapView
{
    /// <summary>Текст окна клетки: заголовок и по одной строке на объект (<see cref="CellObject"/>); числа плиток и серийников — в шестнадцатеричном виде, как в файлах и логах.</summary>
    internal static class CellReport
    {
        public static string OtherMap(int x, int y, int map) =>
            "Клетка (" + x + ", " + y + "): карта " + map + " — данных нет (есть только Фелукка).";

        public static string OutsideMap(int x, int y, int width, int height) =>
            "Клетка (" + x + ", " + y + "): за пределами карты (" + width + " × " + height + "), данных нет.";

        public static string Describe(int x, int y, int map, IReadOnlyList<CellObject> objects)
        {
            var text = new StringBuilder();
            text.Append("Клетка (").Append(x).Append(", ").Append(y).Append(')');
            if (map != EntityStore.UnknownMap)
            {
                text.Append(", карта ").Append(map);
            }

            text.Append(" — объектов: ").Append(objects.Count);
            foreach (var item in objects)
            {
                text.Append('\n').Append(Line(item));
            }

            return text.ToString();
        }

        private static string Line(in CellObject o)
        {
            switch (o.Kind)
            {
                case SceneKind.Land:
                    return "земля: " + Tile(o) + Common(o) + Flags(o);
                case SceneKind.Static:
                    return "статика: " + Tile(o) + Common(o) + Size(o) + Hue(o) + Flags(o);
                case SceneKind.MultiPart:
                    return "часть дома " + Hex8(o.Serial) + ": " + Tile(o) + Common(o) + Size(o) + Flags(o);
                case SceneKind.Item:
                    return "предмет " + Hex8(o.Serial) + ": " + Tile(o) + Common(o) + Size(o) + ", кол-во " + o.Amount + Hue(o) + Flags(o);
                default:
                    return "моб " + Hex8(o.Serial) + ": тело 0x" + o.Id.ToString("X4") + Common(o) + Hue(o);
            }
        }

        private static string Tile(in CellObject o) =>
            "tile 0x" + o.Id.ToString("X4") + (o.Name.Length > 0 ? " «" + o.Name + "»" : string.Empty);

        private static string Common(in CellObject o) => ", Z " + o.Z;

        private static string Size(in CellObject o) =>
            o.Height.HasValue ? ", высота " + o.Height.Value + " (верх " + (o.Z + o.Height.Value) + ")" : string.Empty;

        private static string Hue(in CellObject o) => ", цвет 0x" + o.Hue.ToString("X4");

        private static string Flags(in CellObject o) => ", флаги: " + (o.Flags == TileFlags.None ? "нет" : o.Flags.ToString());

        private static string Hex8(uint serial) => "0x" + serial.ToString("X8");
    }
}
