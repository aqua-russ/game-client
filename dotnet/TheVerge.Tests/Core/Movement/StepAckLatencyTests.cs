using System;
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
    /// Задержка подтверждения шага для экрана отладки (PLAN Стадия 3, п. 9): от отправки запроса (0x02 — шаг или поворот)
    /// до подтверждения этого же запроса (0x22). Сервер отвечает на каждый запрос по порядку
    /// (Projects/Server/Mobiles/Mobile.cs, Move → 0x22; отказ — 0x21), запрос узнаётся по номеру
    /// (Projects/Server/Network/MovementThrottle.cs). Отменённые откатом запросы подтверждения не получают; номер после
    /// отката снова 0 — его время отправки новое. Последняя, средняя и наибольшая задержка — по последним
    /// <see cref="StepAckLatency.Window"/> подтверждениям.
    /// </summary>
    public sealed class StepAckLatencyTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(100, 100, 0);

        private FakeClock _clock = null!;
        private PlayerMotion _motion = null!;
        private StepAckLatency _latency = null!;
        private byte _sequence;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(256, 256, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            var world = new EntityStore();
            world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(256, 256)));
            world.ChangeMap(0);
            _clock = new FakeClock();
            _motion = new PlayerMotion(world, new MovementCheck(map, tiles, null, world), new FakeMovementSender());
            _motion.EnterWorld(Start, Direction.East);
            _latency = new StepAckLatency(_motion, _clock);
            _sequence = 0;
        }

        [TearDown]
        public void TearDown() => _latency.Dispose();

        [Test]
        public void NothingConfirmed_NoSamples()
        {
            Assert.That(_latency.Count, Is.EqualTo(0));
            Assert.That(_latency.LastMs, Is.EqualTo(0));
            Assert.That(_latency.AverageMs, Is.EqualTo(0));
            Assert.That(_latency.MaxMs, Is.EqualTo(0));
        }

        [Test]
        public void StepSentAt100_ConfirmedAt160_Is60()
        {
            SendAt(100, Direction.East);
            ConfirmAt(160);

            Assert.That(_latency.Count, Is.EqualTo(1));
            Assert.That(_latency.LastMs, Is.EqualTo(60));
            Assert.That(_latency.AverageMs, Is.EqualTo(60));
            Assert.That(_latency.MaxMs, Is.EqualTo(60));
        }

        [Test]
        public void TurnAndStep_EachConfirmed_EachMeasuredFromItsOwnSend()
        {
            SendAt(100, Direction.North);
            SendAt(100, Direction.North);
            ConfirmAt(130);
            ConfirmAt(190);

            Assert.That(_latency.Count, Is.EqualTo(2));
            Assert.That(_latency.LastMs, Is.EqualTo(90));
            Assert.That(_latency.AverageMs, Is.EqualTo(60));
            Assert.That(_latency.MaxMs, Is.EqualTo(90));
        }

        [Test]
        public void AverageAndMax_OverLastWindow()
        {
            Assert.That(StepAckLatency.Window, Is.EqualTo(32));
            long now = 1000;
            for (int i = 1; i <= 40; i++)
            {
                SendAt(now, Direction.East);
                ConfirmAt(now + (i <= 8 ? 500 : i));
                now += 1000;
            }

            Assert.That(_latency.Count, Is.EqualTo(40));
            Assert.That(_latency.LastMs, Is.EqualTo(40));
            Assert.That(_latency.MaxMs, Is.EqualTo(40), "задержки по 500 мс ушли из окна");
            Assert.That(_latency.AverageMs, Is.EqualTo(25), "среднее 9…40 — 24,5, округлённо 25");
        }

        [Test]
        public void AfterRollback_NewStepZero_MeasuredFromNewSend()
        {
            SendAt(100, Direction.East);
            SendAt(150, Direction.East);
            _clock.NowMs = 200;
            Assert.That(_motion.Reject(0, Start, Direction.East), Is.True);
            _sequence = 0;

            SendAt(300, Direction.East);
            ConfirmAt(350);

            Assert.That(_latency.Count, Is.EqualTo(1), "отменённые запросы не подтверждаются");
            Assert.That(_latency.LastMs, Is.EqualTo(50), "номер 0 после отката — время новой отправки, а не первой");
        }

        [Test]
        public void ConfirmOfOtherSequence_NotSample()
        {
            SendAt(100, Direction.East);
            _clock.NowMs = 160;

            Assert.That(_motion.Confirm(7), Is.False);

            Assert.That(_latency.Count, Is.EqualTo(0));
        }

        [Test]
        public void AfterDispose_NotCounting()
        {
            _latency.Dispose();

            SendAt(100, Direction.East);
            ConfirmAt(160);

            Assert.That(_latency.Count, Is.EqualTo(0));
        }

        [Test]
        public void SteadyWalking_NoNewMemory()
        {
            // Туда и обратно по одним клеткам, два раза для разгона: мир клиента берёт память при первом заходе в новый участок
            // карты, а рабочие списки проверки шага один раз дорастают до нужного размера на втором проходе.
            WalkThereAndBack(1000);
            WalkThereAndBack(20000);
            long before = GC.GetAllocatedBytesForCurrentThread();

            WalkThereAndBack(40000);

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "запрос, подтверждение и замер — без новой памяти");
            Assert.That(_latency.Count, Is.EqualTo(126));
        }

        // Поворот на запад и 20 шагов, поворот на восток и 20 шагов обратно; сервер подтверждает каждый запрос через 40 мс.
        private void WalkThereAndBack(long from)
        {
            Walk(from, Direction.West, 21);
            Walk(from + 21 * 200L, Direction.East, 21);
        }

        private void Walk(long from, Direction direction, int requests)
        {
            for (int i = 0; i < requests; i++)
            {
                SendAt(from + i * 200L, direction);
                ConfirmAt(from + i * 200L + 40);
            }
        }

        // Помощники без Assert.That на каждом шаге: проверка памяти считает и выделения самого теста.
        private void SendAt(long now, Direction direction)
        {
            _clock.NowMs = now;
            var result = _motion.Step(direction, true);
            if (result != StepResult.Moved && result != StepResult.Turned)
            {
                Assert.Fail("запрос не ушёл: " + result);
            }
        }

        private void ConfirmAt(long now)
        {
            _clock.NowMs = now;
            if (!_motion.Confirm(_sequence))
            {
                Assert.Fail("подтверждение " + _sequence + " не принято");
            }

            _sequence = Next(_sequence);
        }

        private static byte Next(byte sequence) => sequence == 255 ? (byte)1 : (byte)(sequence + 1);
    }
}
