using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Номер карты в снимке мира (PLAN Стадия 2, п. 12: смена карты). Сервер пишет его в дамп полем map —
    /// Projects/UOContent/Custom/TheVerge/Parity/VisibleObjectsDump.cs (ToJson); бот берёт свой из <see cref="EntityStore.MapIndex"/>
    /// (0xBF 0x08). Расхождение по карте — расхождение мира: на другой карте те же клетки означают другое место.
    /// </summary>
    public sealed class WorldSnapshotMapTests
    {
        private static readonly Serial Player = new Serial(0x01);

        private const string EmptyDump = @"{ ""player"": ""0x00000001"", ""map"": 2, ""x"": 10, ""y"": 20, ""z"": 0, ""objects"": [] }";

        [Test]
        public void FromStore_TakesMapFromStore()
        {
            var store = new EntityStore();
            store.ChangeMap(1);

            Assert.That(WorldSnapshot.FromStore(store).Map, Is.EqualTo(1));
        }

        [Test]
        public void FromStore_MapNotReceived_Unknown()
        {
            Assert.That(WorldSnapshot.FromStore(new EntityStore()).Map, Is.EqualTo(EntityStore.UnknownMap));
        }

        [Test]
        public void Read_TakesMapFromServerDump()
        {
            Assert.That(WorldSnapshot.Read(EmptyDump).Map, Is.EqualTo(2));
        }

        [Test]
        public void Read_NoMapField_Unknown()
        {
            var snapshot = WorldSnapshot.Read(@"{ ""player"": ""0x00000001"", ""objects"": [] }");

            Assert.That(snapshot.Map, Is.EqualTo(EntityStore.UnknownMap));
        }

        [Test]
        public void WriteThenRead_KeepsMap()
        {
            var snapshot = new WorldSnapshot { Player = Player, Map = 4 };

            Assert.That(WorldSnapshot.Read(WorldSnapshot.Write(snapshot)).Map, Is.EqualTo(4));
        }

        [Test]
        public void Compare_SameMap_NoDifferences()
        {
            var bot = new WorldSnapshot { Player = Player, Map = 2 };

            Assert.That(WorldSnapshot.Compare(bot, WorldSnapshot.Read(EmptyDump)), Is.Empty);
        }

        [Test]
        public void Compare_OtherMap_NamesBothMaps()
        {
            var bot = new WorldSnapshot { Player = Player, Map = 0 };

            var differences = WorldSnapshot.Compare(bot, WorldSnapshot.Read(EmptyDump));

            Assert.That(differences, Has.Count.EqualTo(1));
            Assert.That(differences[0], Does.Contain("карта").And.Contain("у бота 0").And.Contain("на сервере 2"));
        }

        [Test]
        public void Compare_BotDoesNotKnowMap_Differs()
        {
            var bot = new WorldSnapshot { Player = Player };

            var differences = WorldSnapshot.Compare(bot, WorldSnapshot.Read(EmptyDump));

            Assert.That(differences, Has.Some.Contains("карта").And.Contains("на сервере 2"));
        }
    }
}
