using System.Collections.Generic;
using System.Globalization;
using TheVerge.Core.MapView;
using TheVerge.Core.Ports;
using TheVerge.Core.Session;

namespace TheVerge.Bot
{
    /// <summary>
    /// scene:[радиус] — собрать отладочную 3D-карту (PLAN Стадия 2, п. 13) вокруг бота из его файлов данных (--data) и
    /// написать, сколько в ней блоков. То же строит Unity (<see cref="SceneBuilder"/>); так цепочку можно проверить на
    /// настоящих файлах без Unity.
    /// </summary>
    internal sealed class SceneStep : IBotStep
    {
        private readonly BotWorld _world;
        private readonly ILog _log;
        private readonly BotWorldData? _data;
        private readonly int _radius;

        public SceneStep(BotWorld world, ILog log, BotWorldData? data, string argument)
        {
            _world = world;
            _log = log;
            _data = data;
            TryParseRadius(argument, out _radius);
            Failure = data == null ? "нужна папка файлов данных: --data <папка>." : string.Empty;
        }

        public string TimeoutProblem => "свой персонаж не появился в мире.";

        public string Failure { get; }

        /// <summary>Пусто — радиус по умолчанию (дальность обзора сервера); иначе число от 0 до этой дальности.</summary>
        public static bool TryParseRadius(string text, out int radius)
        {
            radius = ServerLimits.UpdateRange;
            return text.Length == 0 || (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out radius) &&
                                        radius <= ServerLimits.UpdateRange);
        }

        public bool Poll()
        {
            var player = _world.Entities.Player;
            if (player == null || _data == null)
            {
                return false;
            }

            var builder = new SceneBuilder(_data.Map, _data.Tiles, _data.Multis, _world.Entities);
            var blocks = new List<SceneBlock>();
            builder.Build(player.Position.X, player.Position.Y, _radius, blocks);
            _log.Info(SceneSummary.Describe(player.Position.X, player.Position.Y, blocks));
            return true;
        }

        public void Finish()
        {
        }
    }
}
