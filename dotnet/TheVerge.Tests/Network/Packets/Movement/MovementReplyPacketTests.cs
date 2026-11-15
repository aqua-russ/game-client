using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Network.Packets.Movement;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Core.Movement;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Movement
{
    /// <summary>
    /// Ответы сервера о движении — как пишет сервер. 0x22 (Projects/Server/Network/Packets/OutgoingMovementPackets.cs,
    /// SendMovementAck): номер, известность — 3 байта, пример из эталона golden/02-walk-20.tsv: «22 00 03». 0x21
    /// (SendMovementRej): номер, short X, short Y, byte направление, sbyte Z — 8 байт. 0x20 (OutgoingMobilePackets.cs,
    /// SendMobileUpdate) о своём персонаже сервер шлёт, сбросив счёт шагов (NetState.ResetMovementState: телепорт
    /// Mobile.SetLocation, смена тела, цвета, направления Mobile.ProcessDelta, ресинхронизация, вход) — клиент встаёт на эту
    /// клетку. 0xBF 0x08 на другую карту — Mobile.InternalMapChange (сброс счёта, место — в следующем 0x20); на ту же карту
    /// сервер шлёт её при входе ещё раз без сброса (IncomingAccountPackets.DoLogin).
    /// </summary>
    public sealed class MovementReplyPacketTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(10, 10, 0);

        private EntityStore _world = null!;
        private PlayerMotion _motion = null!;
        private MovementReplies _replies = null!;
        private FakeMovementSender _sender = null!;
        private PacketDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(64, 64, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            _world = new EntityStore();
            _world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(64, 64)));
            _world.ChangeMap(0);
            _sender = new FakeMovementSender();
            var log = new RecordingLog();
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), _sender);
            _replies = new MovementReplies(_motion, _sender, log);
            _motion.EnterWorld(Start, Direction.East);
            _dispatcher = new PacketDispatcher(log);
            WorldHandlers.Register(_dispatcher, _world, new WorldEnvironment(), _replies);
            MovementHandlers.Register(_dispatcher, _replies);
        }

        [Test]
        public void Ack_FromGolden_ConfirmsStep_Notoriety()
        {
            _motion.Step(Direction.East, false);

            Assert.That(_dispatcher.Dispatch(new byte[] { 0x22, 0x00, 0x03 }), Is.EqualTo(DispatchResult.Handled));

            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            Assert.That(_world.Player!.Position, Is.EqualTo(new Point3D(11, 10, 0)));
            Assert.That(_world.Player!.Notoriety, Is.EqualTo(3));
        }

        [Test]
        public void Reject_ServerCellAndDirection()
        {
            _motion.Step(Direction.East, true);

            // номер 0, X 10, Y 20, направление 0x84 (South + бег), Z −5
            _dispatcher.Dispatch(new byte[] { 0x21, 0x00, 0x00, 0x0A, 0x00, 0x14, 0x84, 0xFB });

            Assert.That(_motion.Position, Is.EqualTo(new Point3D(10, 20, -5)));
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.South));
            Assert.That(_motion.IsRunning, Is.True);
            Assert.That(_replies.RejectCount, Is.EqualTo(1));
        }

        [Test]
        public void MobileUpdate_OfPlayer_ResetsMovement()
        {
            _motion.Step(Direction.East, true);

            _dispatcher.Dispatch(MobileUpdate(PlayerSerial, 30, 40, 5, Direction.West));

            Assert.That(_motion.Position, Is.EqualTo(new Point3D(30, 40, 5)));
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.West));
            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            _motion.Step(Direction.West, true);
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0), "сервер сбросил счёт шагов");
        }

        [Test]
        public void MobileUpdate_OfOtherMobile_OnlyWorld()
        {
            _motion.Step(Direction.East, true);
            var other = new Serial(0x00000050);

            _dispatcher.Dispatch(MobileUpdate(other, 30, 40, 5, Direction.West));

            Assert.That(_world.FindMobile(other)!.Position, Is.EqualTo(new Point3D(30, 40, 5)));
            Assert.That(_motion.PendingSteps, Is.EqualTo(1));
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(11, 10, 0)));
        }

        [Test]
        public void MapChange_ToOtherMap_WaitsForPosition_SameMapNothing()
        {
            _dispatcher.Dispatch(new byte[] { 0xBF, 0x00, 0x06, 0x00, 0x08, 0x00 });
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved), "та же карта — ходьба идёт");

            _dispatcher.Dispatch(new byte[] { 0xBF, 0x00, 0x06, 0x00, 0x08, 0x01 });

            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.NotInWorld));
            _dispatcher.Dispatch(MobileUpdate(PlayerSerial, 5, 6, 0, Direction.East));
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0));
        }

        // 0x20 как пишет сервер (SendMobileUpdate): Serial, тело, 0, цвет, флаги, X, Y, short 0, направление, Z.
        internal static byte[] MobileUpdate(Serial serial, short x, short y, sbyte z, Direction direction)
        {
            var s = serial.Value;
            return new byte[]
            {
                0x20, (byte)(s >> 24), (byte)(s >> 16), (byte)(s >> 8), (byte)s, 0x01, 0x90, 0x00, 0x83, 0xEA, 0x00,
                (byte)(x >> 8), (byte)x, (byte)(y >> 8), (byte)y, 0x00, 0x00, (byte)direction, (byte)z
            };
        }
    }
}
