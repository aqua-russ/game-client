using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using static TheVerge.Tests.Network.Packets.World.ServerWorldPackets;

namespace TheVerge.Tests.Network.Packets.World
{
    /// <summary>
    /// Пакеты чужих мобов сообщают учётчику показа (PLAN Стадия 3, п. 8): 0x78 — появление и телепорт, 0x77 — шаг с битом бега
    /// 0x80 в направлении (OutgoingMobilePackets.CreateMobileMoving: байт направления — Mobile.Direction как есть), 0x2E на
    /// слой коня — верхом, 0x1D — забыть. Байты — по коду сервера (ServerWorldPackets).
    /// </summary>
    public sealed class OthersMotionPacketTests
    {
        private const uint Player = 0x3E;
        private const uint Orc = 0x100;

        private EntityStore _store = null!;
        private FakeClock _clock = null!;
        private OthersMotion _others = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _clock = new FakeClock { NowMs = 1000 };
            _others = new OthersMotion(_store, _clock);
            _dispatcher = new PacketDispatcher(new RecordingLog());
            WorldHandlers.Register(_dispatcher, _store, new WorldEnvironment(), others: _others);
            _store.Reset(new PlayerState(new Serial(Player), 0x190, new Point3D(633, 858, 0), Direction.North, new MapSize(7168, 4096)));
        }

        [TearDown]
        public void TearDown() => _others.Dispose();

        [Test]
        public void Incoming_Appears_Moving_RunStep_Mounted()
        {
            _dispatcher.Dispatch(MobileIncoming(new Mob { Serial = Orc, X = 640, Y = 860, Z = 0, Direction = 0x02 }));
            Assert.That(_others.TryGetStep(new Serial(Orc), out var standing), Is.True, "0x78 — моб появился");
            Assert.That(standing.To, Is.EqualTo(new Point3D(640, 860, 0)));

            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Orc, X = 641, Y = 860, Z = 1, Direction = 0x82 }));
            _others.Update(1000);

            Assert.That(_others.TryGetStep(new Serial(Orc), out var step), Is.True);
            Assert.That(step.From, Is.EqualTo(new Point3D(640, 860, 0)));
            Assert.That(step.To, Is.EqualTo(new Point3D(641, 860, 1)));
            Assert.That(step.Facing, Is.EqualTo(Direction.East | Direction.Running));
            Assert.That(step.DurationMs, Is.EqualTo(200), "бег пешком");

            _dispatcher.Dispatch(EquipUpdate(0x40000500, 0x3EA2, (byte)Layer.Mount, Orc, 0));
            _clock.NowMs = 1200;
            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Orc, X = 642, Y = 860, Z = 1, Direction = 0x82 }));
            _others.Update(1200);
            _others.TryGetStep(new Serial(Orc), out step);
            Assert.That(step.DurationMs, Is.EqualTo(100), "бег верхом: предмет на слое коня");
        }

        [Test]
        public void Moving_SameCell_NotStep_Incoming_NewCell_Teleport_Remove_Forgets()
        {
            _dispatcher.Dispatch(MobileIncoming(new Mob { Serial = Orc, X = 640, Y = 860 }));
            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Orc, X = 640, Y = 860, Direction = 0x06 }));
            Assert.That(_others.WaitingSteps(new Serial(Orc)), Is.EqualTo(0), "поворот — не шаг");

            _dispatcher.Dispatch(MobileIncoming(new Mob { Serial = Orc, X = 650, Y = 862 }));
            Assert.That(_others.SnapCount, Is.EqualTo(1), "телепорт — перескок");

            _dispatcher.Dispatch(RemoveEntity(Orc));
            Assert.That(_others.TryGetStep(new Serial(Orc), out _), Is.False);
        }

        [Test]
        public void OwnPlayerPackets_NotTracked()
        {
            _dispatcher.Dispatch(MobileMoving(new Mob { Serial = Player, X = 634, Y = 858, Direction = 0x02 }));
            _dispatcher.Dispatch(MobileIncoming(new Mob { Serial = Player, X = 634, Y = 858 }));

            Assert.That(_others.TrackedCount, Is.EqualTo(0));
        }
    }
}
