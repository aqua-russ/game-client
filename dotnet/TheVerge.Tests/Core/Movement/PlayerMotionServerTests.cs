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
    /// Свой персонаж и ответы сервера (PLAN Стадия 3, п. 6). Отказ 0x21 несёт номер, клетку и направление сервера
    /// (Projects/Server/Network/Packets/OutgoingMovementPackets.cs, SendMovementRej); подтверждение 0x22 — номер и
    /// известность своего персонажа (SendMovementAck: Notoriety.Compute(m, m)). Смена карты (Mobile.InternalMapChange) и
    /// ответ на ресинхронизацию (IncomingPlayerPackets.Resynchronize) сбрасывают счёт шагов, а новое место приходит
    /// следом в 0x20 — до него шагать некуда.
    /// </summary>
    public sealed class PlayerMotionServerTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(10, 10, 0);

        private EntityStore _world = null!;
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
            _sender = new FakeMovementSender();
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), _sender);
            _motion.EnterWorld(Start, Direction.East);
        }

        [Test]
        public void EnterWorld_StandsOnLoginCell_FirstStepZero()
        {
            Assert.That(_motion.Position, Is.EqualTo(Start));
            Assert.That(_world.Player!.Position, Is.EqualTo(Start));
            Assert.That(_motion.Step(Direction.East, false), Is.EqualTo(StepResult.Moved));
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0));
        }

        [Test]
        public void Confirm_UpdatesOwnNotoriety()
        {
            _motion.Step(Direction.East, false);

            Assert.That(_motion.Confirm(0, 3), Is.True);

            Assert.That(_world.Player!.Notoriety, Is.EqualTo(3));
        }

        [Test]
        public void Reject_CurrentStep_StandsOnServerCell_NextSequenceZero()
        {
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);
            var server = new Point3D(10, 10, 0);

            Assert.That(_motion.Reject(0, server, Direction.East), Is.True);

            Assert.That(_motion.Position, Is.EqualTo(server));
            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            Assert.That(_world.Player!.Position, Is.EqualTo(server));
            _motion.Step(Direction.East, true);
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0));
        }

        [Test]
        public void Reject_CancelledStep_IsOld_NewPredictionKept()
        {
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);
            _motion.Reject(0, Start, Direction.East);
            _motion.Step(Direction.East, true);
            var predicted = _motion.Position;

            Assert.That(_motion.Reject(1, Start, Direction.East), Is.False, "отказ в отменённом шаге 1 — старый");

            Assert.That(_motion.Position, Is.EqualTo(predicted));
            Assert.That(_motion.PendingSteps, Is.EqualTo(1));
        }

        [Test]
        public void ServerPosition_CancelsSteps_StandsThere()
        {
            _motion.Step(Direction.East, true);
            var teleport = new Point3D(30, 40, 5);

            _motion.ResetToServer(teleport, Direction.South);

            Assert.That(_motion.Position, Is.EqualTo(teleport));
            Assert.That(_motion.StepDirection, Is.EqualTo(Direction.South));
            Assert.That(_motion.Reject(0, Start, Direction.East), Is.False, "шаг до телепорта отменён — его отказ старый");
            Assert.That(_motion.Position, Is.EqualTo(teleport));
        }

        [Test]
        public void MapChanged_NoStepsUntilServerSaysWhere()
        {
            _motion.Step(Direction.East, true);

            _motion.OnMapChanged();

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.NotInWorld));
            Assert.That(_sender.Steps.Count, Is.EqualTo(1));
            _motion.ResetToServer(new Point3D(5, 5, 0), Direction.East);
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0));
        }

        [Test]
        public void Resync_StepsWaitForServerPosition()
        {
            _motion.Step(Direction.East, true);

            _motion.BeginResync();

            Assert.That(_motion.IsResyncing, Is.True);
            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Resyncing));
            _motion.ResetToServer(Start, Direction.East);
            Assert.That(_motion.IsResyncing, Is.False);
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0));
        }

        // Бег пешком при усталости (Projects/UOContent/Misc/StaminaSystem.cs, ProcessPlayerMovement): если выносливость ниже
        // 10% максимума (stamina.additionalLossWhenBelow = 0.1), шаг бега сначала отнимает ещё единицу, и при выносливости
        // 0 бег запрещён (stamina.cannotRunWhenFatigued = True), а шаг — нет (cannotWalkWhenFatigued = False). Клиент тогда
        // сам идёт шагом, как классический клиент (ClassicUO PlayerMobile.Walk: Stamina <= 1 → без бега).
        [TestCase(0, 50, false)]
        [TestCase(1, 50, false)]
        [TestCase(2, 50, true)]
        [TestCase(1, 10, true)]
        [TestCase(5, 100, true)]
        [TestCase(0, 0, true)]
        public void Fatigue_RunTurnsIntoWalk(int stamina, int maximum, bool runs)
        {
            new MobileStatusUpdates(_world).OnVital(PlayerSerial, VitalKind.Stam, new Vital(stamina, maximum));

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved), "персонаж идёт, а не стоит");

            Assert.That(_sender.Last.Run, Is.EqualTo(runs));
            Assert.That(_motion.IsRunning, Is.EqualTo(runs));
        }

        [Test]
        public void Fatigue_MountedOrGhost_StillRuns()
        {
            var status = new MobileStatusUpdates(_world);
            status.OnVital(PlayerSerial, VitalKind.Stam, new Vital(0, 50));
            _world.Equip(new Serial(0x40000010), new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), PlayerSerial, Layer.Mount);

            _motion.Step(Direction.East, true);
            Assert.That(_sender.Last.Run, Is.True, "верхом — своя усталость коня (ProcessMountMovement), выносливость героя не важна");

            _world.Remove(new Serial(0x40000010));
            _world.UpdateMobile(PlayerSerial, new MobileInfo(402, _motion.Position, Direction.East, 0, 0, 1));
            _motion.Step(Direction.East, true);
            Assert.That(_sender.Last.Run, Is.True, "призрака сервер усталостью не проверяет (EventSink_Movement: !Alive)");
        }
    }
}
