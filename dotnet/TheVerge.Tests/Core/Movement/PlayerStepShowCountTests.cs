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
    /// Откаты и перестановки своего персонажа для экрана отладки (PLAN Стадия 3, п. 9; ADR 0020 п. 3): считаются
    /// перескоки, которые видно, — картинка встала на клетку сервера, а не туда, куда клиент предсказал. Откат — по отказу
    /// 0x21, перестановка — по месту от сервера 0x20 (телепорт, ресинхронизация). Отказ в повороте (клетка та же) и 0x20 на
    /// той же клетке картинку не двигают — не считаются; вход в мир — тоже.
    /// </summary>
    public sealed class PlayerStepShowCountTests
    {
        private static readonly Point3D Start = new Point3D(20, 20, 0);

        private FakeClock _clock = null!;
        private PlayerMotion _motion = null!;
        private PlayerStepShow _show = null!;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(128, 128, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            var world = new EntityStore();
            world.Reset(new PlayerState(new Serial(0x00000001), 0x0190, Start, Direction.East, new MapSize(128, 128)));
            world.ChangeMap(0);
            _clock = new FakeClock { NowMs = 1000 };
            _motion = new PlayerMotion(world, new MovementCheck(map, tiles, null, world), new FakeMovementSender());
            _show = new PlayerStepShow(_motion, _clock);
            _motion.EnterWorld(Start, Direction.East);
        }

        [TearDown]
        public void TearDown() => _show.Dispose();

        [Test]
        public void EnterWorld_NotCounted()
        {
            Assert.That(_show.RollbackCount, Is.EqualTo(0));
            Assert.That(_show.RepositionCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectedStep_PictureJumpsBack_CountedAsRollback()
        {
            _motion.Step(Direction.East, true);

            _motion.Reject(0, Start, Direction.East);

            Assert.That(_show.RollbackCount, Is.EqualTo(1));
            Assert.That(_show.RepositionCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectedTurn_SameCell_NotCounted()
        {
            _motion.Step(Direction.North, true);

            Assert.That(_motion.Reject(0, Start, Direction.East), Is.True);

            Assert.That(_show.RollbackCount, Is.EqualTo(0));
        }

        [Test]
        public void ServerPositionElsewhere_CountedAsReposition_SameCellNot()
        {
            _motion.ResetToServer(Start, Direction.East);
            Assert.That(_show.RepositionCount, Is.EqualTo(0), "0x20 на той же клетке картинку не двигает");

            _motion.ResetToServer(new Point3D(40, 40, 0), Direction.East);

            Assert.That(_show.RepositionCount, Is.EqualTo(1));
            Assert.That(_show.RollbackCount, Is.EqualTo(0));
        }

        [Test]
        public void ServerPositionCancellingPredictedStep_CountedAsReposition()
        {
            _motion.Step(Direction.East, true);

            _motion.ResetToServer(Start, Direction.East);

            Assert.That(_show.RepositionCount, Is.EqualTo(1), "предсказанный шаг отменён, картинка вернулась");
        }
    }
}
