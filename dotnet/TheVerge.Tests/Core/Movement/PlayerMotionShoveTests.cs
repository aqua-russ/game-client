using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Status;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Проход сквозь моба (PLAN Стадия 3, п. 4) по Projects/Server/Mobiles/Mobile.cs, CheckShove: на Фелукке
    /// (MapRules.FeluccaRules без FreeMovement) игрок проходит сквозь живого моба, только если выносливость равна максимуму,
    /// и сервер сразу снимает 10 — второй моб подряд уже не пропустит. Выносливость клиент знает только из статуса сервера
    /// (0x11 / 0xA3) и сам не меняет.
    /// </summary>
    public sealed class PlayerMotionShoveTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(10, 10, 0);

        private EntityStore _world = null!;
        private MobileStatusUpdates _status = null!;
        private FakeMovementSender _sender = null!;
        private PlayerMotion _motion = null!;

        [SetUp]
        public void SetUp()
        {
            var map = new FakeMap(64, 64, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            _world = new EntityStore();
            _world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(64, 64)));
            _world.ChangeMap(0);
            _status = new MobileStatusUpdates(_world);
            _sender = new FakeMovementSender();
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), _sender);
            _motion.ResetToServer(Start, Direction.East);
            AddRat(0x00000020, 11);
        }

        [Test]
        public void FullStamina_PassesThroughMobile()
        {
            SetStamina(50, 50);

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(11, 10, 0)));
        }

        [Test]
        public void StaminaNotFull_MobileBlocks()
        {
            SetStamina(40, 50);

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Blocked));
            Assert.That(_sender.Steps, Is.Empty);
        }

        [Test]
        public void NoStatusFromServerYet_MobileBlocks()
        {
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Blocked));
        }

        [Test]
        public void TwoMobilesInARow_SecondBlocksUntilServerAnswersFirst()
        {
            AddRat(0x00000021, 12);
            SetStamina(50, 50);

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved), "первый моб");
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Blocked), "сервер уже снял 10 выносливости");

            _motion.Confirm(0);
            SetStamina(40, 50);
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Blocked), "сервер прислал 40 из 50");

            SetStamina(50, 50);
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved), "выносливость снова полная");
        }

        [Test]
        public void ResetToServer_ForgetsUnansweredShove()
        {
            SetStamina(50, 50);
            _motion.Step(Direction.East, true);

            _motion.ResetToServer(Start, Direction.East);

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved), "отказ: сервер выносливость не снял");
        }

        [Test]
        public void FreeCell_DoesNotSpendShove()
        {
            SetStamina(50, 50);
            _motion.ResetToServer(new Point3D(10, 11, 0), Direction.North);

            Assert.That(_motion.Step(Direction.North, true), Is.EqualTo(StepResult.Moved), "свободная клетка (10, 10)");
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Turned));
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved), "крыса на (11, 10) — первый проход");
        }

        private void AddRat(uint serial, short x) =>
            _world.UpdateMobile(new Serial(serial), new MobileInfo(0x00EE, new Point3D(x, 10, 0), Direction.West, 0, 0, 1));

        private void SetStamina(int current, int maximum) =>
            _status.OnVital(PlayerSerial, VitalKind.Stam, new Vital(current, maximum));
    }
}
