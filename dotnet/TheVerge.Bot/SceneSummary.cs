using System.Collections.Generic;
using TheVerge.Core.MapView;

namespace TheVerge.Bot
{
    /// <summary>Строка для лога бота о блоках отладочной карты (scene:): сколько блоков откуда и какого цвета.</summary>
    public static class SceneSummary
    {
        public static string Describe(int x, int y, IReadOnlyList<SceneBlock> blocks)
        {
            var kinds = new int[5];
            var classes = new int[5];
            foreach (var block in blocks)
            {
                kinds[(int)block.Kind]++;
                classes[(int)block.Class]++;
            }

            return "Сцена вокруг (" + x + ", " + y + "): блоков " + blocks.Count + " — земля " + kinds[(int)SceneKind.Land] +
                   ", статика " + kinds[(int)SceneKind.Static] + ", части домов " + kinds[(int)SceneKind.MultiPart] +
                   "; цвета: вода " + classes[(int)CellClass.Water] + ", стена " + classes[(int)CellClass.Wall] +
                   ", непроходимое " + classes[(int)CellClass.Blocked] + ", поверхность " + classes[(int)CellClass.Surface] +
                   ", прочее " + classes[(int)CellClass.Other] + ".";
        }
    }
}
