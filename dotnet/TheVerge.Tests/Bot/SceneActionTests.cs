using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.MapView;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действие бота scene:[радиус] (PLAN Стадия 2, п. 13): отладочная карта вокруг бота из его файлов данных и строка
    /// об итоге. Радиус — не больше дальности обзора сервера 18 (<c>ServerLimits.UpdateRange</c>).
    /// </summary>
    public sealed class SceneActionTests
    {
        [TestCase("scene:")]
        [TestCase("scene")]
        [TestCase("scene:0")]
        [TestCase("scene:5")]
        [TestCase("scene:18")]
        public void Scene_Parse_NoRadiusOrRadiusUpToViewRange(string text)
        {
            var action = BotAction.Parse(text, out var error);

            Assert.That(action, Is.Not.Null, error);
            Assert.That(action!.Kind, Is.EqualTo(BotActionKind.Scene));
        }

        [TestCase("scene:19")]
        [TestCase("scene:-1")]
        [TestCase("scene:много")]
        [TestCase("scene:5,5")]
        public void Scene_Parse_BadRadius_IsRejected(string text)
        {
            var action = BotAction.Parse(text, out var error);

            Assert.That(action, Is.Null);
            Assert.That(error, Does.Contain("непонятное действие"));
        }

        [Test]
        public void Summary_CountsBlocksByKindAndColor()
        {
            var blocks = new List<SceneBlock>
            {
                new SceneBlock(SceneKind.Land, 1, 1, 0, 5, CellClass.Surface),
                new SceneBlock(SceneKind.Land, 2, 1, 0, 5, CellClass.Water),
                new SceneBlock(SceneKind.Static, 1, 1, 5, 25, CellClass.Wall),
                new SceneBlock(SceneKind.MultiPart, 3, 3, 0, 20, CellClass.Wall),
                new SceneBlock(SceneKind.MultiPart, 3, 4, 0, 1, CellClass.Other),
                new SceneBlock(SceneKind.Static, 4, 4, 0, 4, CellClass.Blocked),
            };

            Assert.That(SceneSummary.Describe(100, 200, blocks),
                Is.EqualTo("Сцена вокруг (100, 200): блоков 6 — земля 2, статика 2, части домов 2; " +
                           "цвета: вода 1, стена 2, непроходимое 1, поверхность 1, прочее 1."));
        }

        [Test]
        public void Summary_NoBlocks_AllZero() =>
            Assert.That(SceneSummary.Describe(0, 0, new List<SceneBlock>()),
                Is.EqualTo("Сцена вокруг (0, 0): блоков 0 — земля 0, статика 0, части домов 0; " +
                           "цвета: вода 0, стена 0, непроходимое 0, поверхность 0, прочее 0."));
    }
}
