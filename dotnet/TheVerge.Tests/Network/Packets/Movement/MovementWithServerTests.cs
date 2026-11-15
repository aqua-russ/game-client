using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Network.Packets.Movement;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Network.Packets.Movement
{
    /// <summary>
    /// Ходьба против фейкового сервера, который ведёт себя как ModernUO (<see cref="FakeMovementServer"/>; PLAN Стадия 3,
    /// п. 6 и критерии «Откаты», «Телепорт, смена карты, толчок»): настоящие байты в обе стороны, обработчики пакетов и
    /// ядро движения. Клиент пробует шагнуть каждый тик (темп — часть 4), ответы идут с задержкой, поэтому после отказа или
    /// телепорта у сервера ещё лежат отменённые шаги. Ожидание: клиент откатывается, следующий шаг — с номером 0, маршрут
    /// доходит до конца без зависаний и ресинхронизаций, в конце клиент и сервер стоят на одной клетке.
    /// </summary>
    public sealed class MovementWithServerTests
    {
        private const uint PlayerSerialValue = 0x00000001;
        private static readonly Point3D Start = new Point3D(20, 20, 0);
        private const int Route = 30;
        private const int TickLimit = 2000;

        private EntityStore _world = null!;
        private NetworkSession _session = null!;
        private PacketDispatcher _dispatcher = null!;
        private PlayerMotion _motion = null!;
        private MovementReplies _replies = null!;
        private FakeMovementServer _server = null!;
        private List<byte> _sentSequences = null!;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(256, 256, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            _world = new EntityStore();
            _world.Reset(new PlayerState(new Serial(PlayerSerialValue), 0x0190, Start, Direction.East, new MapSize(256, 256)));
            _world.ChangeMap(0);
            var log = new RecordingLog();
            _dispatcher = new PacketDispatcher(log);
            _session = new NetworkSession(_dispatcher, log);
            var sender = new MovementSender(_session);
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), sender);
            _replies = new MovementReplies(_motion, sender, log);
            WorldHandlers.Register(_dispatcher, _world, new WorldEnvironment(), _replies);
            MovementHandlers.Register(_dispatcher, _replies);
            _motion.EnterWorld(Start, Direction.East);
            _server = new FakeMovementServer(PlayerSerialValue, Start, Direction.East);
            _sentSequences = new List<byte>();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void RejectMidWalk_RollsBack_NextStepZero_WalkFinishes(int latency)
        {
            _server.LatencyTicks = latency;
            _server.BlockedRequests.Add(10);

            Walk(Direction.East);

            Assert.That(_server.Moves, Is.GreaterThanOrEqualTo(Route));
            Assert.That(_replies.RejectCount, Is.EqualTo(1), "старые отказы на отменённые шаги не считаются");
            Assert.That(_sentSequences.Count(s => s == 0), Is.EqualTo(2), "номер 0 — первый шаг и первый после отказа");
            Assert.That(_sentSequences[10], Is.EqualTo(10));
            AssertSameCell();
        }

        [Test]
        public void RejectMidWalk_StaleStepsDroppedSilently_NoHang()
        {
            _server.LatencyTicks = 3;
            _server.DropStaleSilently = true;
            _server.BlockedRequests.Add(10);

            Walk(Direction.East);

            Assert.That(_server.Moves, Is.GreaterThanOrEqualTo(Route));
            Assert.That(_replies.RejectCount, Is.EqualTo(1));
            AssertSameCell();
        }

        [Test]
        public void TurnAndRejects_SeveralTimes()
        {
            _server.LatencyTicks = 2;
            _server.BlockedRequests.Add(4);
            _server.BlockedRequests.Add(20);
            _server.BlockedRequests.Add(21);

            Walk(Direction.South);

            Assert.That(_server.Moves, Is.GreaterThanOrEqualTo(Route));
            Assert.That(_replies.RejectCount, Is.GreaterThanOrEqualTo(2));
            AssertSameCell();
        }

        [TestCase(0)]
        [TestCase(2)]
        public void TeleportMidWalk_QueueReset_StepsContinue(int latency)
        {
            _server.LatencyTicks = latency;
            var teleport = new Point3D(100, 100, 0);

            Walk(Direction.East, eventAtMove: 10, serverEvent: tick => _server.Teleport(teleport, tick));

            Assert.That(_server.Moves, Is.GreaterThanOrEqualTo(Route));
            Assert.That(_replies.RejectCount, Is.EqualTo(0), "телепорт — не отказ");
            AssertSameCell();
            Assert.That(_server.Position.X, Is.GreaterThan(100), "шаги после телепорта сервер принял");
        }

        [TestCase(0)]
        [TestCase(2)]
        public void MapChangeMidWalk_QueueReset_StepsContinue(int latency)
        {
            _server.LatencyTicks = latency;

            Walk(Direction.East, eventAtMove: 10, serverEvent: tick => _server.ChangeMap(1, new Point3D(150, 30, 0), tick));

            Assert.That(_server.Moves, Is.GreaterThanOrEqualTo(Route));
            Assert.That(_world.MapIndex, Is.EqualTo(1));
            Assert.That(_replies.RejectCount, Is.EqualTo(0));
            AssertSameCell();
        }

        // Шагать каждый тик, пока сервер не выполнит Route шагов, потом дождаться всех ответов. Событие сервера — один раз,
        // когда он выполнит eventAtMove шагов.
        private void Walk(Direction direction, int eventAtMove = -1, System.Action<long>? serverEvent = null)
        {
            long tick = 0;
            bool eventDone = false;
            for (; tick < TickLimit && _server.Moves < Route; tick++)
            {
                _motion.Step(direction, true);
                SendOutgoing(tick);
                _server.Tick(tick, _dispatcher);
                if (!eventDone && serverEvent != null && _server.Moves >= eventAtMove)
                {
                    serverEvent(tick);
                    eventDone = true;
                }
            }

            Assert.That(tick, Is.LessThan(TickLimit), "ходьба зависла: сервер выполнил " + _server.Moves + " шагов из " + Route);
            for (int i = 0; i < 100 && !_server.IsIdle; i++, tick++)
            {
                SendOutgoing(tick);
                _server.Tick(tick, _dispatcher);
            }

            Assert.That(_session.Outgoing.Count, Is.EqualTo(0));
        }

        private void SendOutgoing(long tick)
        {
            for (int i = 0; i < _session.Outgoing.Count; i++)
            {
                var packet = _session.Outgoing.PacketAt(i).ToArray();
                if (packet[0] == MoveRequestPacket.PacketId)
                {
                    _sentSequences.Add(packet[2]);
                }

                _server.FromClient(packet, tick);
            }

            _session.Outgoing.Clear();
        }

        // Клиент дождался всех ответов: он и сервер на одной клетке, ничего не ждёт, мир клиента знает ту же клетку.
        private void AssertSameCell()
        {
            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            Assert.That(_motion.Position, Is.EqualTo(_server.Position));
            Assert.That(_world.Player!.Position, Is.EqualTo(_server.Position));
            Assert.That(_motion.IsResyncing, Is.False);
        }
    }
}
