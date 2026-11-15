using System.Linq;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// Что клиент делает с ответами сервера на шаги (PLAN Стадия 3, п. 6). Отказ — не ошибка клиента: журнал пишет
    /// «Отказ сервера» (для разработчика и бота, игрок его не видит) и считает его. Подтверждение не того шага — рассинхрон:
    /// клиент один раз шлёт запрос ресинхронизации 0x22 (сервер его не ограничивает — IncomingPlayerPackets.Resynchronize,
    /// «TODO: Throttle»), ответы на шаги до него пропускает и стоит, пока сервер не пришлёт своё место (0x20).
    /// </summary>
    public sealed class MovementRepliesTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(10, 10, 0);

        private EntityStore _world = null!;
        private FakeMovementSender _sender = null!;
        private RecordingLog _log = null!;
        private PlayerMotion _motion = null!;
        private MovementReplies _replies = null!;

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
            _log = new RecordingLog();
            _motion = new PlayerMotion(_world, new MovementCheck(map, tiles, null, _world), _sender);
            _replies = new MovementReplies(_motion, _sender, _log);
            _motion.EnterWorld(Start, Direction.East);
        }

        [Test]
        public void Ack_ConfirmsStep_NoResync()
        {
            _motion.Step(Direction.East, true);

            _replies.OnAck(0, 1);

            Assert.That(_world.Player!.Position, Is.EqualTo(new Point3D(11, 10, 0)));
            Assert.That(_sender.Resyncs, Is.EqualTo(0));
            Assert.That(_replies.RejectCount, Is.EqualTo(0));
        }

        [Test]
        public void Reject_RollsBack_LoggedAndCounted_NotAnError()
        {
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);

            _replies.OnReject(0, Start, Direction.East);
            _replies.OnReject(1, Start, Direction.East);

            Assert.That(_motion.Position, Is.EqualTo(Start));
            Assert.That(_replies.RejectCount, Is.EqualTo(1), "отказ в отменённом шаге не считается");
            Assert.That(_log.Entries.Count(e => e.Message.StartsWith("Отказ сервера")), Is.EqualTo(1));
            Assert.That(_log.Errors, Is.Empty);
            Assert.That(_log.Warnings, Is.Empty);
        }

        [Test]
        public void WrongAck_OneResync_WaitsForServerPosition_ThenWalks()
        {
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);

            _replies.OnAck(7, 1);
            _replies.OnAck(1, 1);
            _replies.OnReject(1, Start, Direction.East);
            _replies.OnAck(9, 1);

            Assert.That(_sender.Resyncs, Is.EqualTo(1), "запрос ресинхронизации — один, пока сервер не ответил");
            Assert.That(_replies.ResyncCount, Is.EqualTo(1));
            Assert.That(_replies.RejectCount, Is.EqualTo(0), "ответы до ресинхронизации пропускаются");
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Resyncing));

            var server = new Point3D(12, 10, 0);
            _replies.OnServerPosition(server, Direction.East);

            Assert.That(_motion.Position, Is.EqualTo(server));
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
            Assert.That(_sender.Last.Sequence, Is.EqualTo(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ServerResetDuringFirstStep_OneResync_ThenStepZeroConfirmed(bool mapChange)
        {
            // Сервер сам поставил персонажа (телепорт или смена карты), пока шаг 0 ждал ответа: шаг отменён, но после сброса
            // номер 0 сервер принимает и мог этот шаг выполнить — подтверждение 0 неоднозначно, клиент просит ресинхронизацию.
            _motion.Step(Direction.East, true);
            if (mapChange)
            {
                _replies.OnMapChanged();
            }

            _replies.OnServerPosition(Start, Direction.East);
            _motion.Step(Direction.East, true);
            _replies.OnAck(0, 1);
            Assert.That(_sender.Resyncs, Is.EqualTo(1));

            // Ответ на ресинхронизацию: сервер шлёт 0x20 и только потом сбрасывает счёт (IncomingPlayerPackets.Resynchronize:
            // SendMobileUpdate, затем ResetMovementState). Ответы на все прежние шаги пришли раньше, поэтому следующий шаг 0 —
            // новый, и его подтверждение принимается.
            var server = new Point3D(11, 10, 0);
            _replies.OnServerPosition(server, Direction.East);
            _motion.Step(Direction.East, true);
            _replies.OnAck(0, 1);

            Assert.That(_sender.Resyncs, Is.EqualTo(1), "после ответа на ресинхронизацию шаг 0 подтверждается без новой");
            Assert.That(_motion.PendingSteps, Is.EqualTo(0));
            Assert.That(_world.Player!.Position, Is.EqualTo(new Point3D(12, 10, 0)));
        }

        [Test]
        public void AckWithoutPendingStep_Resync()
        {
            _replies.OnAck(0, 1);

            Assert.That(_sender.Resyncs, Is.EqualTo(1));
        }

        [Test]
        public void ServerPosition_IsNotAReject()
        {
            _motion.Step(Direction.East, true);

            _replies.OnServerPosition(new Point3D(40, 40, 0), Direction.North);
            _replies.OnReject(0, new Point3D(40, 40, 0), Direction.North);

            Assert.That(_replies.RejectCount, Is.EqualTo(0), "телепорт не отказ, а отказ в шаге до него — старый");
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(40, 40, 0)));
        }

        [Test]
        public void MapChanged_StepsWaitForNewPosition()
        {
            _replies.OnMapChanged();

            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.NotInWorld));
            _replies.OnServerPosition(new Point3D(3, 3, 0), Direction.East);
            Assert.That(_motion.Step(Direction.East, true), Is.EqualTo(StepResult.Moved));
        }
    }
}
