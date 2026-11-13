using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Network.Session;
using static TheVerge.Tests.Network.Packets.World.ServerWorldPackets;

namespace TheVerge.Tests.Network.Packets.World
{
    /// <summary>
    /// Предметы на земле, карта и окружение (PLAN Стадия 2, п. 9): 0xF3, 0xBF 0x08, 0xBF 0x18, 0x76, 0x4F, 0x4E, 0xBC,
    /// 0x5B, 0x65, 0x54, 0x6D — байты по коду сервера (ServerWorldPackets и методы, указанные в тестах).
    /// </summary>
    public sealed class WorldObjectPacketTests
    {
        private const uint Player = 0x3E;

        private EntityStore _store = null!;
        private WorldEnvironment _environment = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _environment = new WorldEnvironment();
            _dispatcher = new PacketDispatcher(new RecordingLog());
            WorldHandlers.Register(_dispatcher, _store, _environment);
            _store.Reset(new PlayerState(new Serial(Player), 0x190, new Point3D(1000, 1000, 0), Direction.North, new MapSize(7168, 4096)));
        }

        [Test]
        public void WorldObject_Item_AllFields()
        {
            var packet = WorldObject(new WorldEntity
            {
                Type = 0, Serial = 0x40000500, Graphic = 0x0EED, Amount = 250, X = 1001, Y = 1002, Z = -3, Light = 0x1D, Hue = 0x0481, Flags = 0x20
            });

            Assert.That(_dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.Handled));

            var gold = _store.FindItem(new Serial(0x40000500))!;
            Assert.That(gold.Place, Is.EqualTo(ItemPlace.World));
            Assert.That(gold.Graphic, Is.EqualTo((ushort)0x0EED));
            Assert.That(gold.Amount, Is.EqualTo((ushort)250));
            Assert.That(gold.Position, Is.EqualTo(new Point3D(1001, 1002, -3)));
            Assert.That(gold.Light, Is.EqualTo((byte)0x1D));
            Assert.That(gold.Hue, Is.EqualTo((ushort)0x0481));
            Assert.That(gold.Flags, Is.EqualTo((byte)0x20));
            Assert.That(gold.IsMulti, Is.False);
        }

        [Test]
        public void WorldObject_Multi_MarkedAsMulti()
        {
            _dispatcher.Dispatch(WorldObject(new WorldEntity { Type = 2, Serial = 0x40000600, Graphic = 0x0064, X = 1010, Y = 1010 }));

            var house = _store.FindItem(new Serial(0x40000600))!;
            Assert.That(house.IsMulti, Is.True);
            Assert.That(house.Graphic, Is.EqualTo((ushort)0x0064));
        }

        [Test]
        public void WorldObject_Mobile_UpdatesMobileKeepsDirection()
        {
            _store.UpdateMobile(new Serial(0x100), new MobileInfo(0x11, new Point3D(1, 1, 0), Direction.West, 0, 0, 3));

            _dispatcher.Dispatch(WorldObject(new WorldEntity { Type = 1, Serial = 0x100, Graphic = 0x0012, X = 1003, Y = 1004, Z = 2, Hue = 0x21 }));

            var mob = _store.FindMobile(new Serial(0x100))!;
            Assert.That(mob.Body, Is.EqualTo((ushort)0x0012));
            Assert.That(mob.Position, Is.EqualTo(new Point3D(1003, 1004, 2)));
            Assert.That(mob.Direction, Is.EqualTo(Direction.West));
            Assert.That(mob.Notoriety, Is.EqualTo((byte)3));
        }

        [Test]
        public void WorldObject_MobileSerialAsItem_Rejected()
        {
            _dispatcher.Dispatch(WorldObject(new WorldEntity { Type = 0, Serial = 0x100, Graphic = 0x0EED }));

            Assert.That(_store.FindMobile(new Serial(0x100)), Is.Null);
            Assert.That(_store.Metrics.Rejected, Is.EqualTo(1));
        }

        [Test]
        public void MapChange_SameTwiceAtLogin_ThenOther_Clears()
        {
            _dispatcher.Dispatch(MapChange(0));
            _dispatcher.Dispatch(WorldObject(new WorldEntity { Serial = 0x40000500, Graphic = 0x0EED, X = 1000, Y = 1000 }));
            _dispatcher.Dispatch(MapChange(0));
            Assert.That(_store.FindItem(new Serial(0x40000500)), Is.Not.Null);

            _dispatcher.Dispatch(MapChange(1));

            Assert.That(_store.MapIndex, Is.EqualTo(1));
            Assert.That(_store.FindItem(new Serial(0x40000500)), Is.Null);
            Assert.That(_store.Player, Is.Not.Null);
        }

        [Test]
        public void MapPatches_CountsPerMap()
        {
            _dispatcher.Dispatch(MapPatches(10, 20, 0, 0, 3, 4, 0, 0));

            Assert.That(_environment.GetStaticPatches(0), Is.EqualTo(10));
            Assert.That(_environment.GetLandPatches(0), Is.EqualTo(20));
            Assert.That(_environment.GetStaticPatches(2), Is.EqualTo(3));
            Assert.That(_environment.GetLandPatches(2), Is.EqualTo(4));
            Assert.That(_environment.GetStaticPatches(9), Is.EqualTo(0));
        }

        [Test]
        public void MapPatches_CountBeyondLength_Skipped()
        {
            var packet = MapPatches(10, 20);
            packet[8] = 50; // число карт 50, а данных — на одну

            Assert.That(_dispatcher.Dispatch(packet), Is.EqualTo(DispatchResult.ReadError));
            Assert.That(_environment.GetStaticPatches(0), Is.EqualTo(0));
        }

        [Test]
        public void ServerChange_PlayerMovedMapSizeStored()
        {
            _dispatcher.Dispatch(ServerChange(1500, 1600, -5, 7168, 4096));

            Assert.That(_store.Player!.Position, Is.EqualTo(new Point3D(1500, 1600, -5)));
            Assert.That(_environment.MapSize.Width, Is.EqualTo(7168));
            Assert.That(_environment.MapSize.Height, Is.EqualTo(4096));
        }

        [Test]
        public void LightSeasonTimeWeather_Stored()
        {
            _dispatcher.Dispatch(new byte[] { 0x4F, 0x1A });                          // SendGlobalLightLevel
            _dispatcher.Dispatch(new byte[] { 0x4E, 0x00, 0x00, 0x00, 0x3E, 0x05 });  // SendPersonalLightLevel
            _dispatcher.Dispatch(new byte[] { 0xBC, 0x03, 0x01 });                    // SendSeasonChange: зима, со звуком
            _dispatcher.Dispatch(new byte[] { 0x5B, 13, 45, 7 });                     // SendCurrentTime
            _dispatcher.Dispatch(new byte[] { 0x65, 0x02, 0x46, 0xF6 });              // SendWeather: снег, плотность, −10

            Assert.That(_environment.GlobalLight, Is.EqualTo((byte)0x1A));
            Assert.That(_environment.PersonalLight, Is.EqualTo((byte)0x05));
            Assert.That(_environment.Season, Is.EqualTo((byte)3));
            Assert.That(new[] { _environment.Hour, _environment.Minute, _environment.Second }, Is.EqualTo(new[] { 13, 45, 7 }));
            Assert.That(_environment.WeatherType, Is.EqualTo((byte)2));
            Assert.That(_environment.WeatherDensity, Is.EqualTo((byte)0x46));
            Assert.That(_environment.Temperature, Is.EqualTo((sbyte)-10));
        }

        [Test]
        public void PersonalLight_OtherSerial_Ignored()
        {
            _dispatcher.Dispatch(new byte[] { 0x4E, 0x00, 0x00, 0x01, 0x00, 0x09 });

            Assert.That(_environment.PersonalLight, Is.EqualTo((byte)0));
        }

        [Test]
        public void SoundAndMusic_Counted()
        {
            _dispatcher.Dispatch(PlaySound(0x0057, 1000, 1000, 0));
            _dispatcher.Dispatch(PlaySound(0x0058, 1000, 1000, 0));
            _dispatcher.Dispatch(new byte[] { 0x6D, 0x00, 0x2A }); // SendPlayMusic
            Assert.That(_environment.SoundsPlayed, Is.EqualTo(2));
            Assert.That(_environment.LastSound, Is.EqualTo((ushort)0x58));
            Assert.That(_environment.Music, Is.EqualTo((ushort)0x2A));

            _dispatcher.Dispatch(new byte[] { 0x6D, 0x1F, 0xFF }); // SendStopMusic
            Assert.That(_environment.Music, Is.EqualTo(WorldEnvironment.NoMusic));
        }

        [Test]
        public void Changes_RaiseEvent()
        {
            var changes = 0;
            _environment.Changed += () => changes++;

            _dispatcher.Dispatch(new byte[] { 0x4F, 0x1A });
            _dispatcher.Dispatch(new byte[] { 0x5B, 13, 45, 7 });

            Assert.That(changes, Is.EqualTo(2));
        }
    }
}
