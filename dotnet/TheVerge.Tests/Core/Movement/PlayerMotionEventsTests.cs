using System.Collections.Generic;
using NUnit.Framework;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Tests.Core.MapView;

namespace TheVerge.Tests.Core.Movement
{
    /// <summary>
    /// О чём своё движение сообщает показу (ADR 0020 пп. 1–3, 5): шаг предсказан — персонаж уже на новой клетке (событие
    /// <see cref="PlayerMotion.Moved"/>: откуда, куда, направление с битом бега, верхом); сервер сам поставил персонажа —
    /// событие <see cref="PlayerMotion.Placed"/> с клеткой и причиной: вход в мир (0x1B), отказ (0x21 — откат на клетку и
    /// направление сервера, OutgoingMovementPackets.SendMovementRej), место от сервера (0x20). Поворот на месте клетку не
    /// меняет (Projects/Server/Mobiles/Mobile.cs, Move) — не шаг; старый отказ в отменённом шаге ничего не меняет.
    /// </summary>
    public sealed class PlayerMotionEventsTests
    {
        private static readonly Serial PlayerSerial = new Serial(0x00000001);
        private static readonly Point3D Start = new Point3D(10, 10, 0);

        private EntityStore _world = null!;
        private FakeMap _map = null!;
        private PlayerMotion _motion = null!;
        private List<StepMove> _moved = null!;
        private List<(Point3D Cell, Direction Facing, SnapReason Reason)> _placed = null!;

        [SetUp]
        public void SetUp()
        {
            _map = new FakeMap(64, 64, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            tiles.SetItem(0x0013, TileFlags.Impassable | TileFlags.Wall, 20, "wall");
            _world = new EntityStore();
            _world.Reset(new PlayerState(PlayerSerial, 0x0190, Start, Direction.East, new MapSize(64, 64)));
            _world.ChangeMap(0);
            _motion = new PlayerMotion(_world, new MovementCheck(_map, tiles, null, _world), new FakeMovementSender());
            _moved = new List<StepMove>();
            _placed = new List<(Point3D, Direction, SnapReason)>();
            _motion.Moved += move => _moved.Add(move);
            _motion.Placed += (cell, facing, reason) => _placed.Add((cell, facing, reason));
        }

        [Test]
        public void EnterWorld_PlacedAsAppeared()
        {
            Assert.That(_motion.IsPlaced, Is.False);

            _motion.EnterWorld(Start, Direction.East | Direction.Running);

            Assert.That(_motion.IsPlaced, Is.True);
            Assert.That(_placed, Is.EqualTo(new[] { (Start, Direction.East, SnapReason.Appeared) }));
            Assert.That(_moved, Is.Empty);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Step_ReportsFromToFacingRunAndMount(bool mounted, bool run)
        {
            if (mounted)
            {
                _world.Equip(new Serial(0x40000010), new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), PlayerSerial, Layer.Mount);
            }

            _motion.EnterWorld(Start, Direction.East);

            Assert.That(_motion.Step(Direction.East, run), Is.EqualTo(StepResult.Moved));

            var expectedFacing = run ? Direction.East | Direction.Running : Direction.East;
            Assert.That(_moved, Has.Count.EqualTo(1));
            Assert.That(_moved[0].From, Is.EqualTo(Start));
            Assert.That(_moved[0].To, Is.EqualTo(new Point3D(11, 10, 0)));
            Assert.That(_moved[0].Facing, Is.EqualTo(expectedFacing));
            Assert.That(_moved[0].IsRunning, Is.EqualTo(run));
            Assert.That(_moved[0].IsMounted, Is.EqualTo(mounted));
        }

        [Test]
        public void TurnOrBlockedStep_IsNotMove()
        {
            _map.AddStatic(10, 9, new StaticTile(0x0013, 0, 0));
            _motion.EnterWorld(Start, Direction.East);

            Assert.That(_motion.Step(Direction.North, true), Is.EqualTo(StepResult.Turned));
            Assert.That(_motion.Step(Direction.North, true), Is.EqualTo(StepResult.Blocked));

            Assert.That(_moved, Is.Empty);
            Assert.That(_placed, Has.Count.EqualTo(1), "только вход в мир");
        }

        [Test]
        public void Reject_PlacedAsRejected_OnServerCellAndFacing()
        {
            _motion.EnterWorld(Start, Direction.East);
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);

            Assert.That(_motion.Reject(0, Start, Direction.North), Is.True);

            Assert.That(_placed[1], Is.EqualTo((Start, Direction.North, SnapReason.Rejected)));
            Assert.That(_motion.Position, Is.EqualTo(Start));
        }

        [Test]
        public void StaleReject_NotPlaced()
        {
            _motion.EnterWorld(Start, Direction.East);
            _motion.Step(Direction.East, true);
            _motion.Step(Direction.East, true);
            _motion.Reject(0, Start, Direction.East);
            _motion.Step(Direction.East, true);

            Assert.That(_motion.Reject(1, Start, Direction.East), Is.False, "шаг 1 отменён откатом — его отказ старый");

            Assert.That(_placed, Has.Count.EqualTo(2), "вход и один настоящий отказ");
            Assert.That(_motion.Position, Is.EqualTo(new Point3D(11, 10, 0)), "новое предсказание осталось");
        }

        [Test]
        public void ServerPosition_PlacedAsRepositioned()
        {
            _motion.EnterWorld(Start, Direction.East);
            var there = new Point3D(30, 40, 5);

            _motion.ResetToServer(there, Direction.South | Direction.Running);

            Assert.That(_placed[1], Is.EqualTo((there, Direction.South, SnapReason.Repositioned)));
        }

        [Test]
        public void MapChange_NotPlacedUntilServerSaysWhere()
        {
            _motion.EnterWorld(Start, Direction.East);

            _motion.OnMapChanged();

            Assert.That(_motion.IsPlaced, Is.False);
            Assert.That(_placed, Has.Count.EqualTo(1), "клетки на новой карте ещё нет — ставить некуда");
        }
    }
}
