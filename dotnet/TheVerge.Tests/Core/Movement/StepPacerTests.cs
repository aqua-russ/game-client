using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Status;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Темп шагов (PLAN Стадия 3, пп. 5, 7). Сервер ведёт расписание шагов накопительно: каждый шаг сдвигает время
    /// следующего на задержку (Projects/Server/Mobiles/Mobile.cs, Move: _nextMovementTime += cost), поворот бесплатный и
    /// ставит расписание на «сейчас» (cost == 0). Клиент шлёт шаги не раньше своего расписания, построенного так же: k-й шаг
    /// непрерывной ходьбы — не раньше первого плюс k задержек и не позже чем через один тик после этого (опоздание из-за
    /// тика до ClientLimits.MoveCatchUpMs догоняется, большее — например, игрок стоял — не копится). Кредит сервера на
    /// джиттер (movementThrottle.maxCredit) клиент не использует. Поворот и шаг уходят в одном тике (CONTROLS §4), но не
    /// раньше срока шага.
    /// </summary>
    public sealed class StepPacerTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(20, 20, 0);

        private FakeClock _clock = null!;
        private EntityStore _world = null!;
        private FakeMovementSender _sender = null!;
        private PlayerMotion _motion = null!;
        private MoveIntent _intent = null!;
        private StepPacer _pacer = null!;
        private List<long> _sentAt = null!;
        private bool _serverAnswers;
        private long _pacerAllocated;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(512, 512, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            _world = new EntityStore();
            _world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(512, 512)));
            _world.ChangeMap(0);
            _clock = new FakeClock();
            _sender = new FakeMovementSender();
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), _sender);
            _motion.EnterWorld(Start, Direction.East);
            _intent = new MoveIntent(_clock, CameraLayout.NorthUp);
            _pacer = new StepPacer(_intent, _motion);
            _sentAt = new List<long>();
            _serverAnswers = true;
        }

        [TestCase(16, false, true, 200)]
        [TestCase(33, false, true, 200)]
        [TestCase(16, false, false, 400)]
        [TestCase(16, true, true, 100)]
        [TestCase(33, true, true, 100)]
        [TestCase(16, true, false, 200)]
        public void ContinuousWalk_OnServerSchedule_NeitherFasterNorDrifting(int tickMs, bool mounted, bool run, int delayMs)
        {
            if (mounted)
            {
                _world.Equip(new Serial(0x40000010), new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), PlayerSerial, Layer.Mount);
            }

            if (!run)
            {
                _intent.ToggleRun();
            }

            _intent.Press(MoveKeys.Right);
            RunFor(60 * delayMs, tickMs);

            Assert.That(_sentAt.Count, Is.GreaterThanOrEqualTo(55));
            for (int k = 1; k < _sentAt.Count; k++)
            {
                long scheduled = _sentAt[0] + (long)k * delayMs;
                Assert.That(_sentAt[k], Is.GreaterThanOrEqualTo(scheduled), "шаг " + k + " раньше расписания сервера");
                Assert.That(_sentAt[k], Is.LessThan(scheduled + tickMs), "шаг " + k + " отстал от расписания больше чем на тик");
            }

            Assert.That(_sender.Steps.TrueForAll(s => s.Run == run && s.Direction == Direction.East));
        }

        [Test]
        public void TurnAndStep_SameTick_ThenFullDelay()
        {
            _motion.EnterWorld(Start, Direction.North);
            _intent.Press(MoveKeys.Right);

            RunFor(400, 16);

            Assert.That(_sender.Steps.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(_sender.Steps[0], Is.EqualTo(new FakeMovementSender.Sent(Direction.East, true, 0)), "поворот");
            Assert.That(_sender.Steps[1], Is.EqualTo(new FakeMovementSender.Sent(Direction.East, true, 1)), "шаг");
            Assert.That(_sentAt[1], Is.EqualTo(_sentAt[0]), "поворот и шаг — в одном тике");
            Assert.That(_sentAt[2] - _sentAt[1], Is.GreaterThanOrEqualTo(200), "после поворота шаг — через полную задержку");
            Assert.That(_motion.Position.X, Is.GreaterThan(Start.X));
        }

        [Test]
        public void TurnWhileRunning_WaitsForStepTime()
        {
            _intent.Press(MoveKeys.Right);
            RunFor(450, 16);
            int sentBefore = _sentAt.Count;

            // Нажатие сразу после шага: вверх при зажатой «вправо» — диагональ (Right), второй клавиши она не ждёт.
            _intent.Press(MoveKeys.Up);
            _intent.Release(MoveKeys.Right);
            RunFor(400, 16);

            Assert.That(sentBefore, Is.EqualTo(2));
            Assert.That(_sentAt.Count, Is.GreaterThanOrEqualTo(sentBefore + 2));
            Assert.That(_sender.Steps[sentBefore].Direction, Is.EqualTo(Direction.Right), "поворот");
            Assert.That(_sentAt[sentBefore], Is.GreaterThanOrEqualTo(_sentAt[0] + sentBefore * 200L),
                "поворот не сокращает задержку прошлого шага: он уходит в срок следующего шага");
            Assert.That(_sentAt[sentBefore + 1], Is.EqualTo(_sentAt[sentBefore]), "и шаг за ним — в том же тике");
        }

        [Test]
        public void Pause_DoesNotBank_FirstStepAtOnce_SecondAfterFullDelay()
        {
            _intent.Press(MoveKeys.Right);
            RunFor(500, 16);
            _intent.Release(MoveKeys.Right);
            RunFor(1000, 16);
            int sentBefore = _sentAt.Count;

            _intent.Press(MoveKeys.Right);
            long pressedAt = _clock.NowMs;
            RunFor(500, 16);

            Assert.That(sentBefore, Is.GreaterThan(0));
            Assert.That(_sentAt.Count, Is.GreaterThanOrEqualTo(sentBefore + 2));
            Assert.That(_sentAt[sentBefore] - pressedAt, Is.LessThan(ClientLimits.MovePairWaitMs + 16), "первый шаг — сразу после ожидания второй клавиши");
            Assert.That(_sentAt[sentBefore + 1] - _sentAt[sentBefore], Is.GreaterThanOrEqualTo(200), "стоял — запас не копится");
        }

        [Test]
        public void QueueFull_StepWaits_TapNotLost()
        {
            _serverAnswers = false;
            _intent.Press(MoveKeys.Right);
            RunFor(1000, 16);
            Assert.That(_sender.Steps.Count, Is.EqualTo(ClientLimits.MaxUnacknowledgedSteps), "без ответов — не больше предела");

            _intent.Release(MoveKeys.Right);
            _intent.Press(MoveKeys.Up);
            _intent.Release(MoveKeys.Up);
            RunFor(200, 16);
            Assert.That(_sender.Steps.Count, Is.EqualTo(ClientLimits.MaxUnacknowledgedSteps));

            AnswerAll();
            RunFor(32, 16);

            Assert.That(_sender.Steps.Count, Is.EqualTo(ClientLimits.MaxUnacknowledgedSteps + 2), "тап дал поворот и шаг");
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.North));
        }

        [Test]
        public void TurnWithOneFreeSlot_StepGoesWhenSlotFrees()
        {
            _serverAnswers = false;
            _intent.Press(MoveKeys.Right);
            RunFor(300, 16);
            Assert.That(_sender.Steps.Count, Is.EqualTo(2));

            _intent.Release(MoveKeys.Right);
            _intent.Press(MoveKeys.Up);
            RunFor(400, 16);
            Assert.That(_sender.Steps.Count, Is.EqualTo(3), "поворот занял последнее место");
            Assert.That(_sender.Last.Direction, Is.EqualTo(Direction.North));

            _intent.Release(MoveKeys.Up);
            _motion.Confirm(_sender.Steps[0].Sequence);
            long freedAt = _clock.NowMs;
            RunFor(16, 16);

            Assert.That(_sender.Steps.Count, Is.EqualTo(4), "шаг после поворота ушёл, как только освободилось место");
            Assert.That(_sentAt[3], Is.EqualTo(freedAt));
            Assert.That(_motion.Position.Y, Is.EqualTo(Start.Y - 1));
        }

        [Test]
        public void Fatigue_WalksAtWalkingPace()
        {
            new MobileStatusUpdates(_world).OnVital(PlayerSerial, VitalKind.Stam, new Vital(0, 50));
            _intent.Press(MoveKeys.Right);

            RunFor(2000, 16);

            Assert.That(_sentAt.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(_sender.Steps.TrueForAll(s => !s.Run), "уставший идёт шагом");
            Assert.That(_sentAt[1] - _sentAt[0], Is.GreaterThanOrEqualTo(400), "и в темпе шага");
        }

        [Test]
        public void SteadyWalking_PacerDoesNotAllocate()
        {
            _intent.Press(MoveKeys.Right);
            _sentAt.Capacity = 4096;
            RunFor(2000, 16);

            _pacerAllocated = 0;
            RunFor(4000, 16);

            Assert.That(_sentAt.Count, Is.GreaterThan(20));
            Assert.That(_pacerAllocated, Is.EqualTo(0), "темп, намерение и шаг — без новой памяти (мир клиента считается отдельно)");
        }

        private void RunFor(int durationMs, int tickMs)
        {
            long end = _clock.NowMs + durationMs;
            while (_clock.NowMs < end)
            {
                int before = _sender.Steps.Count;
                long allocated = System.GC.GetAllocatedBytesForCurrentThread();
                _pacer.Update(_clock.NowMs);
                _pacerAllocated += System.GC.GetAllocatedBytesForCurrentThread() - allocated;
                for (int i = before; i < _sender.Steps.Count; i++)
                {
                    _sentAt.Add(_clock.NowMs);
                }

                if (_serverAnswers)
                {
                    AnswerAll();
                }

                _clock.NowMs += tickMs;
            }
        }

        private void AnswerAll()
        {
            while (_motion.PendingSteps > 0)
            {
                int index = _sender.Steps.Count - _motion.PendingSteps;
                _motion.Confirm(_sender.Steps[index].Sequence);
            }
        }
    }
}
