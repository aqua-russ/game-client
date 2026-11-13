using NUnit.Framework;
using TheVerge.Bot;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Действие mapdump:&lt;клеток&gt;,&lt;seed&gt;,&lt;папка&gt;[,all-statics] (PLAN Стадия 2, п. 11б): бот говорит
    /// «[DumpMapData клеток seed [all-statics]» (Custom/TheVerge/Parity/DumpMapDataCommand.cs сервера) и сверяет файл.
    /// </summary>
    public sealed class MapDumpRequestTests
    {
        [Test]
        public void Parse_CellsSeedFolder()
        {
            Assert.That(MapDumpRequest.TryParse("10000,42,/tmp/a b/parity", out var request), Is.True);
            Assert.That(request.Cells, Is.EqualTo(10000));
            Assert.That(request.Seed, Is.EqualTo(42));
            Assert.That(request.Directory, Is.EqualTo("/tmp/a b/parity"));
            Assert.That(request.AllStatics, Is.False);
            Assert.That(request.Command, Is.EqualTo("[DumpMapData 10000 42"));
        }

        [Test]
        public void Parse_AllStatics()
        {
            Assert.That(MapDumpRequest.TryParse("5,-7,/p,all-statics", out var request), Is.True);
            Assert.That(request.AllStatics, Is.True);
            Assert.That(request.Directory, Is.EqualTo("/p"));
            Assert.That(request.Command, Is.EqualTo("[DumpMapData 5 -7 all-statics"));
        }

        [TestCase("")]
        [TestCase("10,1")]
        [TestCase("0,1,/p")]
        [TestCase("1000001,1,/p")]
        [TestCase("x,1,/p")]
        [TestCase("10,y,/p")]
        [TestCase("10,1,")]
        public void Parse_Bad(string text)
        {
            Assert.That(MapDumpRequest.TryParse(text, out _), Is.False);
        }

        [Test]
        public void Action_MapDump()
        {
            var action = BotAction.Parse("mapdump:100,1,/p", out var error);

            Assert.That(error, Is.Empty);
            Assert.That(action!.Kind, Is.EqualTo(BotActionKind.MapDump));
            Assert.That(BotAction.Parse("mapdump:abc", out error), Is.Null);
            Assert.That(error, Does.Contain("mapdump:abc"));
        }
    }
}
