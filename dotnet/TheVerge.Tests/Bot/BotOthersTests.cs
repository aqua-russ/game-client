using System.Linq;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Movement;
using TheVerge.Core.Primitives;
using TheVerge.Tests.Core.Movement;
using TheVerge.Tests.Network.Session;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Как бот видит чужих мобов (PLAN Стадия 3, п. 8; tools/check/others.sh): по каждому — сколько шагов пришло, показано и
    /// выброшено, средний интервал прихода против длительности показа по режимам (паузы дольше полутора шагов в темп не
    /// идут), догонялки, перескоки по причинам и появления.
    /// </summary>
    public sealed class BotOthersTests
    {
        private static readonly Serial Orc = new Serial(0x100);
        private static readonly Point3D Cell = new Point3D(10, 10, 0);

        [Test]
        public void Stats_TempoPerMode_PausesSkipped_SnapsCounted()
        {
            var stats = new OtherStats(0x0190);
            stats.Add(new MotionSnap(Cell, SnapReason.Appeared, 0, 900));
            for (int i = 0; i < 5; i++)
            {
                stats.Add(Step(1000 + i * 200, 1000 + i * 200, 200, 1, running: true));
            }

            stats.Add(new MotionSnap(Cell, SnapReason.Teleported, 1, 1900));
            for (int i = 0; i < 3; i++)
            {
                stats.Add(Step(4000 + i * 404, 4000 + i * 404, 400, 1, running: false));
            }

            Assert.That(stats.Describe(Orc), Is.EqualTo(
                "Чужой 0x00000100 тело 0x0190: пришло 9, показано 8, выброшено 1; шаг пешком 404/400 мс ×3, бег пешком " +
                "200/200 мс ×5; догонялок 0, перескоков 1 (телепорт 1, рывок 0, отставание 0), появлений 1."));
        }

        // Сервер ждёт после шага столько, сколько стоит этот шаг (Projects/Server/Mobiles/Mobile.cs, Move:
        // _nextMovementTime += cost): интервал до следующего прихода — темп предыдущего шага, его и сравниваем с его показом.
        [Test]
        public void Stats_IntervalBelongsToPreviousStep_RunThenWalk()
        {
            var stats = new OtherStats(0x0190);
            stats.Add(Step(1000, 1000, 200, 1, running: true));
            stats.Add(Step(1200, 1200, 200, 1, running: true));
            for (int i = 0; i < 3; i++)
            {
                stats.Add(Step(1400 + i * 400, 1400 + i * 400, 400, 1, running: false));
            }

            Assert.That(stats.Describe(Orc), Does.Contain("шаг пешком 400/400 мс ×3, бег пешком 200/200 мс ×2;"));
        }

        [Test]
        public void Stats_CatchUps_MountedRun_NoArrivalPairs()
        {
            var stats = new OtherStats(0x00EE);
            stats.Add(Step(1000, 1000, 50, 2, running: true, mounted: true));
            stats.Add(new MotionSnap(Cell, SnapReason.Backlog, 3, 1100));
            stats.Add(new MotionSnap(Cell, SnapReason.Jumped, 0, 1200));

            Assert.That(stats.Describe(Orc), Is.EqualTo(
                "Чужой 0x00000100 тело 0x00EE: пришло 4, показано 1, выброшено 3; бег верхом —/50 мс ×1; догонялок 1, перескоков 2 " +
                "(телепорт 0, рывок 1, отставание 1), появлений 0."));
        }

        [Test]
        public void BotOthers_ListsOtherMobiles_BySerial_WithBody()
        {
            using var scene = new OthersScene();
            using var others = new BotOthers(scene.Motion, scene.Store);
            scene.Appear(OthersScene.Rat, 205, 205);
            scene.Appear(OthersScene.Orc, 200, 200);
            scene.Move(OthersScene.Orc, 201, 200, Direction.East | Direction.Running);
            scene.Tick(0);
            var log = new RecordingLog();

            others.WriteTo(log);

            var lines = log.Entries.Select(e => e.Message).ToList();
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines[0], Does.StartWith("Чужой 0x00000100 тело 0x0190: пришло 1, показано 1"));
            Assert.That(lines[1], Does.StartWith("Чужой 0x00000200 тело 0x0190: пришло 0, показано 0"));
        }

        private static ShownStep Step(long arrived, long start, int duration, int behind, bool running, bool mounted = false)
        {
            var facing = running ? Direction.East | Direction.Running : Direction.East;
            return new ShownStep(new StepMove(Cell, Cell, facing, mounted), arrived, start, duration, behind);
        }
    }
}
