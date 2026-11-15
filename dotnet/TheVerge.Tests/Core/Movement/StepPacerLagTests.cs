using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Core.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Темп своих шагов под пингом (PLAN Стадия 3, пп. 5, 7, 9; ADR 0020, цель «темп равен серверной задержке при задержке до
    /// 200 мс»). Сервер отвечает на каждый запрос — шаг и поворот (Projects/Server/Mobiles/Mobile.cs, Move → 0x22), здесь
    /// через 430 мс: 200 мс туда, 200 обратно, разброс и такт кадра (режим «симуляция пинга», 200 / 200). Без ответа клиент
    /// держит не больше <see cref="ClientLimits.MaxUnacknowledgedSteps"/> запросов: столько же, сколько ClassicUO
    /// (src/ClassicUO.Client/Game/Constants.cs, MAX_STEP_COUNT = 5; поворот — такой же запрос, PlayerMobile.Walk), и вдвое
    /// меньше предела сервера (movementThrottle.hardQueueLimit = 10, Projects/Server/Network/MovementThrottle.cs). Тогда при
    /// таком пинге бег верхом идёт по расписанию сервера, а поворот и шаг за ним уходят в одном тике (CONTROLS §4).
    /// </summary>
    public sealed class StepPacerLagTests
    {
        private const int RoundTripMs = 430;
        private const int TickMs = 16;
        private static readonly Point3D Start = new Point3D(100, 300, 0);

        private FakeClock _clock = null!;
        private EntityStore _world = null!;
        private FakeMovementSender _sender = null!;
        private PlayerMotion _motion = null!;
        private MoveIntent _intent = null!;
        private StepPacer _pacer = null!;
        private Queue<(byte Sequence, long SentMs)> _inFlight = null!;
        private List<long> _sentAt = null!;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(512, 512, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            _world = new EntityStore();
            _world.Reset(new PlayerState(new Serial(0x00000001), 0x0190, Start, Direction.East, new MapSize(512, 512)));
            _world.ChangeMap(0);
            _clock = new FakeClock { NowMs = 1000 };
            _sender = new FakeMovementSender();
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), _sender);
            _motion.EnterWorld(Start, Direction.East);
            _intent = new MoveIntent(_clock, CameraLayout.NorthUp);
            _pacer = new StepPacer(_intent, _motion);
            _inFlight = new Queue<(byte, long)>();
            _sentAt = new List<long>();
            _motion.Requested += sequence =>
            {
                _inFlight.Enqueue((sequence, _clock.NowMs));
                _sentAt.Add(_clock.NowMs);
            };
        }

        [Test]
        public void Limit_LikeClassicUo_WellBelowServerQueueReset()
        {
            Assert.That(ClientLimits.MaxUnacknowledgedSteps, Is.EqualTo(5));
            Assert.That(ServerLimits.ServerStepQueueLimit, Is.EqualTo(10));
        }

        [Test]
        public void MountedRun_Ping430_KeepsServerSchedule()
        {
            _world.Equip(new Serial(0x40000010), new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), new Serial(0x00000001), Layer.Mount);
            _intent.Press(MoveKeys.Right);

            RunFor(3000);

            Assert.That(_sentAt.Count, Is.GreaterThanOrEqualTo(28));
            for (int k = 1; k < _sentAt.Count; k++)
            {
                long scheduled = _sentAt[0] + (long)k * ServerLimits.RunMountDelayMs;
                Assert.That(_sentAt[k], Is.LessThan(scheduled + TickMs), "шаг " + k + " ждал ответа сервера, а не расписания");
            }
        }

        [Test]
        public void RunThenDiagonal_Ping430_TurnAndStepInSameTick()
        {
            _intent.Press(MoveKeys.Right);
            RunFor(2000);
            int before = _sentAt.Count;
            long lastStraight = _sentAt[before - 1];

            _intent.Press(MoveKeys.Up);
            RunFor(600);

            Assert.That(_sender.Steps[before].Direction, Is.EqualTo(Direction.Right), "поворот на северо-восток");
            Assert.That(_sender.Steps[before + 1].Direction, Is.EqualTo(Direction.Right), "шаг на северо-восток");
            Assert.That(_sentAt[before], Is.LessThan(lastStraight + ServerLimits.RunFootDelayMs + TickMs),
                "поворот — в срок следующего шага");
            Assert.That(_sentAt[before + 1], Is.EqualTo(_sentAt[before]), "шаг после поворота не ждёт ответа сервера");
        }

        // Тики по 16 мс; сервер подтверждает запросы по порядку через RoundTripMs после отправки.
        private void RunFor(long durationMs)
        {
            long end = _clock.NowMs + durationMs;
            while (_clock.NowMs < end)
            {
                _clock.NowMs += TickMs;
                long now = _clock.NowMs;
                while (_inFlight.Count > 0 && _inFlight.Peek().SentMs + RoundTripMs <= now)
                {
                    Assert.That(_motion.Confirm(_inFlight.Dequeue().Sequence), Is.True);
                }

                _pacer.Update(now);
            }
        }
    }
}
