using System.Globalization;
using TheVerge.Core.MapView;
using TheVerge.Core.Ports;

namespace TheVerge.Bot
{
    /// <summary>
    /// inspect:[x,y] — написать в журнал окно клетки (<see cref="CellInspector"/>, PLAN Стадия 2, п. 13) из файлов данных
    /// (--data) и мира бота: то же, что Unity показывает по клику. Без клетки — клетка бота. Строка журнала — на каждый
    /// объект, чтобы сценарии могли искать нужную по тексту.
    /// </summary>
    internal sealed class InspectStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly BotWorldData? _data;
        private readonly bool _hasCell;
        private readonly int _x;
        private readonly int _y;

        public InspectStep(BotWorld world, ILog log, BotWorldData? data, string argument)
        {
            _world = world;
            _log = log;
            _data = data;
            TryParseCell(argument, out _hasCell, out _x, out _y);
            Failure = data == null ? "нужна папка файлов данных: --data <папка>." : string.Empty;
        }

        public string TimeoutProblem => "свой персонаж не появился в мире.";

        public string Failure { get; }

        /// <summary>Пусто — клетка бота (<paramref name="hasCell"/> = false); иначе «x,y» — два числа от 0.</summary>
        public static bool TryParseCell(string text, out bool hasCell, out int x, out int y)
        {
            hasCell = text.Length > 0;
            x = y = 0;
            if (!hasCell)
            {
                return true;
            }

            var parts = text.Split(',');
            return parts.Length == 2 &&
                   int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out x) &&
                   int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out y);
        }

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (player == null || _data == null)
            {
                return false;
            }

            var inspector = new CellInspector(_data.Map, _data.Tiles, _data.Multis, _world.Entities);
            var x = _hasCell ? _x : player.Position.X;
            var y = _hasCell ? _y : player.Position.Y;
            foreach (var line in inspector.Describe(x, y).Split('\n'))
            {
                _log.Info(line);
            }

            return true;
        }

        public void Finish()
        {
        }
    }
}
