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
    /// Отклик на нажатие для экрана отладки (PLAN Стадия 3, п. 9; ADR 0020, цель «отклик не зависит от пинга»): от нажатия
    /// клавиши движения из покоя (ни одна не была зажата) до первого запроса серверу — поворота или шага. Одиночная клавиша
    /// ждёт вторую <see cref="ClientLimits.MovePairWaitMs"/> (PLAN 3.1), пара — уходит сразу; ответ сервера не ждётся
    /// (шаг предсказывается). Зажатая клавиша — один отклик, а не по отклику на шаг.
    /// </summary>
    public sealed class StepPacerResponseTests
    {
        private static readonly Point3D Start = new Point3D(20, 20, 0);

        private FakeClock _clock = null!;
        private FakeMap _map = null!;
        private PlayerMotion _motion = null!;
        private MoveIntent _intent = null!;
        private StepPacer _pacer = null!;
        private byte _confirmed;

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(128, 128, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            tiles.SetItem(0x0013, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            var world = new EntityStore();
            world.Reset(new PlayerState(new Serial(0x00000001), 0x0190, Start, Direction.East, new MapSize(128, 128)));
            world.ChangeMap(0);
            _clock = new FakeClock();
            _motion = new PlayerMotion(world, new MovementCheck(_map, tiles, null, world), new FakeMovementSender());
            _motion.EnterWorld(Start, Direction.East);
            _intent = new MoveIntent(_clock, CameraLayout.NorthUp);
            _pacer = new StepPacer(_intent, _motion);
            _confirmed = 0;
        }

        [Test]
        public void NoPressYet_NoResponse()
        {
            Tick(1000);

            Assert.That(_pacer.ResponseCount, Is.EqualTo(0));
            Assert.That(_pacer.LastResponseMs, Is.EqualTo(-1));
        }

        [Test]
        public void SingleKey_WaitsForPairKey_ResponseIsPairWait()
        {
            Assert.That(ClientLimits.MovePairWaitMs, Is.EqualTo(50));
            _clock.NowMs = 1000;
            _intent.Press(MoveKeys.Right);

            Tick(1000);
            Tick(1032);
            Assert.That(_pacer.ResponseCount, Is.EqualTo(0), "ждёт вторую клавишу");

            Tick(1050);

            Assert.That(_pacer.ResponseCount, Is.EqualTo(1));
            Assert.That(_pacer.LastResponseMs, Is.EqualTo(50));
        }

        [Test]
        public void PairOfKeys_ResponseInSameTick()
        {
            _clock.NowMs = 1000;
            _intent.Press(MoveKeys.Up);
            _intent.Press(MoveKeys.Right);

            Tick(1000);

            Assert.That(_pacer.ResponseCount, Is.EqualTo(1));
            Assert.That(_pacer.LastResponseMs, Is.EqualTo(0), "поворот на северо-восток ушёл в том же тике");
        }

        [Test]
        public void HeldKey_OneResponse_NotPerStep()
        {
            _clock.NowMs = 1000;
            _intent.Press(MoveKeys.Right);
            for (long t = 1000; t <= 3000; t += 16)
            {
                Tick(t);
                _motion.Confirm(LastSentSequence());
            }

            Assert.That(_motion.MovesSent, Is.GreaterThan(5));
            Assert.That(_pacer.ResponseCount, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseAndPressAgain_SecondResponse()
        {
            _clock.NowMs = 1000;
            _intent.Press(MoveKeys.Right);
            Tick(1050);
            _intent.Release(MoveKeys.Right);
            _motion.Confirm(LastSentSequence());
            Tick(2000);

            _clock.NowMs = 3000;
            _intent.Press(MoveKeys.Up);
            _intent.Press(MoveKeys.Left);
            Tick(3010);

            Assert.That(_pacer.ResponseCount, Is.EqualTo(2));
            Assert.That(_pacer.LastResponseMs, Is.EqualTo(10));
        }

        [Test]
        public void FacingWall_NoRequest_NoResponse()
        {
            _map.AddStatic(21, 20, new StaticTile(0x0013, 0, 0));
            _clock.NowMs = 1000;
            _intent.Press(MoveKeys.Right);

            Tick(1100);

            Assert.That(_motion.MovesSent, Is.EqualTo(0));
            Assert.That(_pacer.ResponseCount, Is.EqualTo(0), "в стену запрос не ушёл — отклика нет");
        }

        private void Tick(long now)
        {
            _clock.NowMs = now;
            _pacer.Update(now);
        }

        // Самый старый ждущий шаг: в этих тестах сервер подтверждает сразу, поэтому ждёт не больше одного.
        private byte LastSentSequence()
        {
            if (_motion.PendingSteps == 0)
            {
                return 255;
            }

            byte sequence = _confirmed;
            _confirmed = sequence == 255 ? (byte)1 : (byte)(sequence + 1);
            return sequence;
        }
    }
}
