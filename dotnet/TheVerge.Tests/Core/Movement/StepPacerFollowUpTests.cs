using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Шаг после поворота (PLAN Стадия 3, п. 7; CONTROLS §4): если поворот занял последнее место в очереди шагов, шаг ждёт
    /// освободившегося места. Пока он ждёт, сервер мог переставить персонажа (телепорт, смена карты, отказ, ответ на
    /// ресинхронизацию), а игрок — открыть чат: тогда шаг относился бы к прежнему месту и шёл бы мимо воли игрока — он
    /// отбрасывается. Короткое нажатие (поворот и шаг) без таких событий — StepPacerTests.
    /// </summary>
    public sealed class StepPacerFollowUpTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(20, 20, 0);
        private static readonly Point3D There = new Point3D(50, 50, 0);

        private FakeClock _clock = null!;
        private FakeMovementSender _sender = null!;
        private PlayerMotion _motion = null!;
        private MoveIntent _intent = null!;
        private StepPacer _pacer = null!;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(512, 512, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            var world = new EntityStore();
            world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(512, 512)));
            world.ChangeMap(0);
            _clock = new FakeClock();
            _sender = new FakeMovementSender();
            _motion = new PlayerMotion(world, new MovementCheck(map, tiles, null, world), _sender);
            _motion.EnterWorld(Start, Direction.East);
            _intent = new MoveIntent(_clock, CameraLayout.NorthUp);
            _pacer = new StepPacer(_intent, _motion);
        }

        [TestCase("teleport")]
        [TestCase("reject")]
        [TestCase("mapchange")]
        [TestCase("resync")]
        public void ServerMovedPlayer_StepAfterTurnDropped(string reset)
        {
            TurnIntoFullQueue();
            switch (reset)
            {
                case "teleport":
                    _motion.ResetToServer(There, Direction.North);
                    break;
                case "reject":
                    _motion.Reject(_sender.Steps[0].Sequence, There, Direction.North);
                    break;
                case "mapchange":
                    _motion.OnMapChanged();
                    _motion.ResetToServer(There, Direction.North);
                    break;
                default:
                    _motion.BeginResync();
                    _motion.ResetToServer(There, Direction.North);
                    break;
            }

            RunFor(200);

            Assert.That(_sender.Steps.Count, Is.EqualTo(3), "шаг к прежнему месту не ушёл");
            Assert.That(_motion.Position, Is.EqualTo(There));
        }

        [Test]
        public void InputBlocked_StepAfterTurnDropped()
        {
            TurnIntoFullQueue();
            _intent.SetBlocked(true);
            _motion.Confirm(_sender.Steps[0].Sequence);

            RunFor(200);

            Assert.That(_sender.Steps.Count, Is.EqualTo(3));
            Assert.That(_motion.Position.Y, Is.EqualTo(Start.Y));
        }

        [Test]
        public void NothingHappened_StepAfterTurnGoesWhenSlotFrees()
        {
            TurnIntoFullQueue();
            _motion.Confirm(_sender.Steps[0].Sequence);

            RunFor(16);

            Assert.That(_sender.Steps.Count, Is.EqualTo(4));
            Assert.That(_motion.Position.Y, Is.EqualTo(Start.Y - 1));
        }

        // Бег вправо без ответов сервера, затем тап вверх: поворот занимает последнее место в очереди, шаг после него ждёт.
        private void TurnIntoFullQueue()
        {
            _intent.Press(MoveKeys.Right);
            RunFor(300);
            _intent.Release(MoveKeys.Right);
            _intent.Press(MoveKeys.Up);
            RunFor(400);
            _intent.Release(MoveKeys.Up);
            Assert.That(_sender.Steps.Count, Is.EqualTo(3), "поворот занял последнее место");
            Assert.That(_sender.Last.Direction, Is.EqualTo(Direction.North));
        }

        private void RunFor(int durationMs)
        {
            long end = _clock.NowMs + durationMs;
            for (; _clock.NowMs < end; _clock.NowMs += 16)
            {
                _pacer.Update(_clock.NowMs);
            }
        }
    }
}
