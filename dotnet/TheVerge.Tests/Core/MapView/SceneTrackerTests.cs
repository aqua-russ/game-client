using NUnit.Framework;
using TheVerge.Core.MapView;
using TheVerge.Core.Primitives;
using TheVerge.Core.World;
using static TheVerge.Tests.Core.World.WorldSamples;

namespace TheVerge.Tests.Core.MapView
{
    /// <summary>
    /// Когда пересобирать отладочную карту: когда свой персонаж перешёл на другую клетку, сменилась карта, мир очищен или
    /// появился/исчез дом или корабль. Перемещение чужих мобов и обычных предметов карту не трогает.
    /// </summary>
    public sealed class SceneTrackerTests
    {
        private static readonly Serial House = new Serial(0x40000100);
        private EntityStore _store = null!;
        private SceneTracker _tracker = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new EntityStore();
            _tracker = new SceneTracker(_store);
        }

        [TearDown]
        public void TearDown() => _tracker.Dispose();

        private bool Take(out int x, out int y, out int map) => _tracker.TryTakeRebuild(out x, out y, out map);

        private bool Take() => Take(out _, out _, out _);

        private void EnterWorld(short x = 100, short y = 100)
        {
            _store.Reset(PlayerAt(x, y));
            _store.ChangeMap(0);
            Take();
        }

        private static ItemInfo HouseInfo(short x = 105, short y = 105) =>
            new ItemInfo(0x10, 1, new Point3D(x, y, 0), 0, 0, 0, isMulti: true);

        [Test]
        public void NoPlayerYet_NothingToBuild() => Assert.That(Take(), Is.False);

        [Test]
        public void PlayerAppears_RebuildOnce_WithItsCell()
        {
            _store.Reset(PlayerAt(120, 130));
            _store.ChangeMap(0);

            Assert.That(Take(out var x, out var y, out var map), Is.True);
            Assert.That((x, y, map), Is.EqualTo((120, 130, 0)));
            Assert.That(Take(), Is.False);
        }

        [Test]
        public void PlayerStepsToOtherCell_Rebuild()
        {
            EnterWorld();

            _store.MovePlayerTo(new Point3D(101, 100, 0));

            Assert.That(Take(out var x, out var y, out _), Is.True);
            Assert.That((x, y), Is.EqualTo((101, 100)));
        }

        [Test]
        public void PlayerChangesOnlyHeight_NoRebuild()
        {
            EnterWorld();

            _store.MovePlayerTo(new Point3D(100, 100, 7));

            Assert.That(Take(), Is.False);
        }

        [Test]
        public void OtherMobileMoves_NoRebuild()
        {
            EnterWorld();
            _store.UpdateMobile(Orc, Mob(101, 101));
            _store.MoveMobile(Orc, Mob(102, 101));

            Assert.That(Take(), Is.False);
        }

        [Test]
        public void OrdinaryItemPlacedAndRemoved_NoRebuild()
        {
            EnterWorld();
            _store.PlaceInWorld(Apple, Thing(0x09D0, 101, 101));
            _store.Remove(Apple);

            Assert.That(Take(), Is.False);
        }

        [Test]
        public void MultiAppears_Rebuild()
        {
            EnterWorld();

            _store.PlaceInWorld(House, HouseInfo());

            Assert.That(Take(), Is.True);
            Assert.That(Take(), Is.False);
        }

        [Test]
        public void MultiRemoved_Rebuild()
        {
            EnterWorld();
            _store.PlaceInWorld(House, HouseInfo());
            Take();

            _store.Remove(House);

            Assert.That(Take(), Is.True);
        }

        [Test]
        public void WorldCleared_Rebuild()
        {
            EnterWorld();

            _store.Clear(keepPlayer: true);

            Assert.That(Take(), Is.True);
        }

        [Test]
        public void MapChanged_RebuildWithNewMap()
        {
            EnterWorld();

            _store.ChangeMap(1);

            Assert.That(Take(out _, out _, out var map), Is.True);
            Assert.That(map, Is.EqualTo(1));
        }

        [Test]
        public void NewLogin_Rebuild()
        {
            EnterWorld();

            _store.Reset(PlayerAt(300, 300));
            _store.ChangeMap(0);

            Assert.That(Take(out var x, out var y, out _), Is.True);
            Assert.That((x, y), Is.EqualTo((300, 300)));
        }

        [Test]
        public void AfterDispose_EventsAreIgnored()
        {
            EnterWorld();
            _tracker.Dispose();

            _store.PlaceInWorld(House, HouseInfo());

            Assert.That(Take(), Is.False);
        }
    }
}
