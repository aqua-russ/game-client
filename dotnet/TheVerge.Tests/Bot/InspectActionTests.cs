using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действие бота inspect:[x,y] (PLAN Стадия 2, п. 13): написать в журнал окно клетки — то же, что Unity показывает по
    /// клику. Без клетки — клетка бота. Сам отчёт проверен тестами <c>CellInspector</c>; здесь — разбор действия, сама клетка — проверка <c>debugmap.sh</c>.
    /// </summary>
    public sealed class InspectActionTests
    {
        [TestCase("inspect:")]
        [TestCase("inspect")]
        [TestCase("inspect:1496,1628")]
        [TestCase("inspect:0,0")]
        public void Inspect_Parse_NoCellOrTwoNumbers(string text)
        {
            var action = BotAction.Parse(text, out var error);

            Assert.That(action, Is.Not.Null, error);
            Assert.That(action!.Kind, Is.EqualTo(BotActionKind.Inspect));
        }

        [TestCase("inspect:5")]
        [TestCase("inspect:a,b")]
        [TestCase("inspect:1,2,3")]
        [TestCase("inspect:-1,5")]
        [TestCase("inspect:1,")]
        public void Inspect_Parse_BadCell_IsRejected(string text)
        {
            var action = BotAction.Parse(text, out var error);

            Assert.That(action, Is.Null);
            Assert.That(error, Does.Contain("непонятное действие"));
        }
    }
}
