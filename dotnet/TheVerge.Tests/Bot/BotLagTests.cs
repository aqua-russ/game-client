using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using TheVerge.Bot;
using TheVerge.Core.Chat;
using TheVerge.Core.Diagnostics;
using TheVerge.Core.Items;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Targeting;
using TheVerge.Core.Tiles;
using TheVerge.Core.World;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Transport;
using TheVerge.Tests.Core.MapView;
using TheVerge.Tests.Core.Movement;
using TheVerge.Tests.Core.Session;
using TheVerge.Tests.Network.Session;
using TheVerge.Tests.Network.Transport;

namespace TheVerge.Tests.Bot
{
    /// <summary>
    /// Подключение симуляции лага и замеров к боту (PLAN Стадия 3, п. 12): без задержки маршрут совпадает с
    /// <c>walk-route</c>; при заданной задержке измеренное подтверждение шага (0x02 → 0x22) не меньше заданной; темп
    /// считается относительно серверной задержки; при всплеске лага до 500 мс бот-наблюдатель догоняет чужого моба без
    /// лишних перескоков.
    /// </summary>
    public sealed class BotLagTests
    {
        private static readonly Serial Player = new Serial(0x3E);
        private static readonly Serial Other = new Serial(0x100);
        private static readonly Point3D Start = new Point3D(100, 100, 0);
        private const int TickMs = 16;

        [Test]
        public void LagDisabled_RouteMatchesBaselineExactly()
        {
            var off = RunRoute(new LagSimulation());
            var explicitZero = RunRoute(new LagSimulation { Enabled = true, IncomingDelayMs = 0, IncomingJitterMs = 0, OutgoingDelayMs = 0, OutgoingJitterMs = 0 });

            Assert.That(off.Steps, Is.EqualTo(explicitZero.Steps));
            Assert.That(off.End, Is.EqualTo(explicitZero.End));
            Assert.That(off.Rejects, Is.EqualTo(0));
        }

        [Test]
        public void Lag100EachWay_MeasuredAckLatencyAtLeastConfiguredDelay()
        {
            var lag = new LagSimulation
            {
                Enabled = true,
                IncomingDelayMs = 100,
                IncomingJitterMs = 0,
                OutgoingDelayMs = 100,
                OutgoingJitterMs = 0,
            };

            var run = RunRoute(lag);

            Assert.That(run.Rejects, Is.EqualTo(0));
            Assert.That(run.Stats.AckCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(run.Stats.MinAckMs, Is.GreaterThanOrEqualTo(200), "100 туда + 100 обратно");
            Assert.That(run.Stats.AvgAckMs, Is.GreaterThanOrEqualTo(200));
            Assert.That(run.Summary, Does.Contain("отказов сервера 0").And.Contain("подтверждение шага: мин "));
        }

        [Test]
        public void LaggedConnection_InBotRunner_DelaysServerMessagesByConfiguredLag()
        {
            using var server = new LocalServer();
            var clock = new StopwatchClock();
            var log = new RecordingLog();
            var lag = new LagSimulation { Enabled = true, IncomingDelayMs = 100, IncomingJitterMs = 0, OutgoingDelayMs = 100, OutgoingJitterMs = 0 };
            var login = new BotLogin("127.0.0.1", server.Port, new LoginCredentials("player", "secret"), string.Empty);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var run = System.Threading.Tasks.Task.Run(() => new BotRunner(() => new LaggedConnection(
                new TcpConnection(new CountingArrayPool(), ClientLimits.ConnectTimeoutMs, ClientLimits.InboundQueueBytes),
                clock, lag, new Random(1), new CountingArrayPool()), clock, log, null).Run(login, CancellationToken.None));

            server.Accept();
            server.ReadExactly(21 + 62);
            server.Client.Send(new byte[] { 0x82, 0x03 });
            server.CloseClient();

            Assert.That(run.Wait(5000), Is.True);
            Assert.That(run.Result, Is.EqualTo(BotExitCode.LoginRejected));
            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(200), "100 туда + 100 обратно");
        }

        [Test]
        public void GotoAndRandomWalk_ReportTempoRelativeToServerDelay()
        {
            var setup = CreateMovementWorld();
            var script = Script(setup.World, setup.Log, setup.Clock, "goto:110,100,run", "randomwalk:20,7,90,90,120,120,mix");

            for (int i = 0; i < 1000 && !script.IsDone && script.Problem.Length == 0; i++)
            {
                setup.Movement.Pacer.Update(setup.Clock.NowMs);
                while (setup.Movement.Motion.PendingSteps > 0)
                {
                    var sent = setup.Sender.Steps[setup.Sender.Steps.Count - setup.Movement.Motion.PendingSteps];
                    setup.Movement.Replies.OnAck(sent.Sequence, 1);
                }

                script.Step();
                setup.Clock.NowMs += 20;
            }

            Assert.That(script.IsDone, Is.True, script.Problem);
            var text = string.Join("\n", setup.Log.Entries.Select(e => e.Message));
            Assert.That(text, Does.Contain("Темп: фактическое время "));
            Assert.That(text, Does.Contain("сервер 1800 мс"));
            Assert.That(text, Does.Contain("шага/с"));
        }

        [Test]
        public void WaitStepsAndLagSpike500Ms_OtherMobile_CatchesUpWithZeroSnaps()
        {
            var (world, others, botOthers, lag, clock, log) = CreateOthersWorld();
            var script = Script(world, log, clock, "waitsteps:0x0190,4", "lagspike:500");

            RunMountedOtherWithSpike(world.Entities, others, lag, clock, script);

            Assert.That(script.IsDone, Is.True, script.Problem);
            botOthers.WriteTo(log);
            var text = string.Join("\n", log.Entries.Select(e => e.Message));
            Assert.That(text, Does.Contain("Всплеск входящего лага: 500 мс."));
            Assert.That(others.CatchUpCount, Is.GreaterThan(0));
            Assert.That(others.SnapCount, Is.EqualTo(0));
            Assert.That(text, Does.Contain("перескоков 0 (телепорт 0, рывок 0, отставание 0)"));
            botOthers.Dispose();
        }

        private static (BotWorld World, OthersMotion Others, BotOthers BotOthers, LagSimulation Lag, FakeClock Clock, RecordingLog Log)
            CreateOthersWorld()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var log = new RecordingLog();
            var lag = new LagSimulation();
            var store = new EntityStore();
            store.Reset(new PlayerState(Player, 0x0190, Start, Direction.East, new MapSize(512, 512)));
            store.ChangeMap(0);
            var others = new OthersMotion(store, clock);
            var botOthers = new BotOthers(others, store);
            var world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store),
                new ItemCommands(store, new NoItems(), clock), new SpeechCommands(new NoSpeech(), clock),
                new TargetCursor(new NoTarget()))
            {
                Others = botOthers,
                Lag = lag,
            };
            store.UpdateMobile(Other, new MobileInfo(0x0190, new Point3D(105, 100, 0), Direction.East, 0, 0, 1));
            store.Equip(new Serial(0x40000101), new ItemInfo(0x3EA2, 1, Point3D.Zero, 0, 0, 0), Other, Layer.Mount);
            others.OnIncoming(Other);
            return (world, others, botOthers, lag, clock, log);
        }

        private static void RunMountedOtherWithSpike(EntityStore store, OthersMotion others, LagSimulation lag, FakeClock clock, BotScript script)
        {
            var heldSteps = new List<(long ReleaseMs, short X)>();
            long holdUntil = 0;
            for (int tick = 0; tick < 250 && !script.IsDone && script.Problem.Length == 0; tick++)
            {
                clock.NowMs += 20;
                long now = clock.NowMs;
                int spike = lag.TakeIncomingSpikeMs();
                if (spike > 0)
                {
                    holdUntil = now + spike;
                }

                if (tick % 5 == 0 && tick / 5 >= 1 && tick / 5 <= 18)
                {
                    heldSteps.Add((Math.Max(now, holdUntil), (short)(105 + tick / 5)));
                }

                DeliverHeld(store, others, heldSteps, now);
                others.Update(now);
                script.Step();
            }
        }

        private static void DeliverHeld(EntityStore store, OthersMotion others, List<(long ReleaseMs, short X)> held, long now)
        {
            while (held.Count > 0 && held[0].ReleaseMs <= now)
            {
                var s = held[0];
                held.RemoveAt(0);
                store.UpdateMobile(Other, new MobileInfo(0x0190, new Point3D(s.X, 100, 0), Direction.East | Direction.Running, 0, 0, 1));
                others.OnMoving(Other);
            }
        }

        private static (List<Point3D> Steps, Point3D End, int Rejects, MovementStats Stats, string Summary) RunRoute(LagSimulation lag)
        {
            var setup = CreateMovementWorld();
            var visited = new List<Point3D>();
            setup.Movement.Motion.Moved += m => visited.Add(m.To);
            var script = Script(setup.World, setup.Log, setup.Clock, "goto:106,104,run", "goto:110,104,walk");
            var inFlightOut = new Queue<(long ReleaseMs, byte Seq)>();
            var inFlightIn = new Queue<(long ReleaseMs, byte Seq)>();

            for (int tick = 0; tick < 2000 && !script.IsDone && script.Problem.Length == 0; tick++)
            {
                setup.Clock.NowMs += TickMs;
                long now = setup.Clock.NowMs;
                int before = setup.Sender.Steps.Count;
                setup.Movement.Pacer.Update(now);
                for (int i = before; i < setup.Sender.Steps.Count; i++)
                {
                    long arriveAtServer = now + (lag.Enabled ? lag.OutgoingDelayMs : 0);
                    inFlightOut.Enqueue((arriveAtServer, setup.Sender.Steps[i].Sequence));
                }

                while (inFlightOut.Count > 0 && inFlightOut.Peek().ReleaseMs <= now)
                {
                    var req = inFlightOut.Dequeue();
                    long arriveAtClient = req.ReleaseMs + (lag.Enabled ? lag.IncomingDelayMs : 0);
                    inFlightIn.Enqueue((arriveAtClient, req.Seq));
                }

                while (inFlightIn.Count > 0 && inFlightIn.Peek().ReleaseMs <= now)
                {
                    setup.Movement.Replies.OnAck(inFlightIn.Dequeue().Seq, 1);
                }

                script.Step();
            }

            Assert.That(script.IsDone, Is.True, script.Problem);
            return (visited, setup.Store.Player!.Position, setup.Movement.Replies.RejectCount, setup.Movement.Stats,
                setup.Movement.Stats.Describe());
        }

        private static (BotWorld World, BotMovement Movement, FakeMovementSender Sender, EntityStore Store, FakeClock Clock, RecordingLog Log)
            CreateMovementWorld()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var log = new RecordingLog();
            var sender = new FakeMovementSender();
            var store = new EntityStore();
            store.Reset(new PlayerState(Player, 0x0190, Start, Direction.East, new MapSize(512, 512)));
            store.ChangeMap(0);
            var map = new FakeMap(512, 512, new LandTile(3, 0));
            var tiles = new FakeTiles();
            tiles.SetLand(3, TileFlags.None, "grass");
            var motion = new PlayerMotion(store, new MovementCheck(map, tiles, null, store), sender);
            var movement = new BotMovement(motion, new MovementReplies(motion, sender, log), clock);
            movement.OnPlayerConfirmed(new PlayerState(Player, 0x0190, Start, Direction.East, new MapSize(512, 512)));
            var world = new BotWorld(store, new WorldEnvironment(), new ContainerContents(store),
                new ItemCommands(store, new NoItems(), clock), new SpeechCommands(new NoSpeech(), clock),
                new TargetCursor(new NoTarget()))
            {
                Movement = movement,
            };
            return (world, movement, sender, store, clock, log);
        }

        private static BotScript Script(BotWorld world, ILog log, IClock clock, params string[] actions) =>
            new BotScript(actions.Select(a => BotAction.Parse(a, out _)!).ToList(), world, log, clock);

        private sealed class NoItems : IItemSender { public void SendUse(Serial serial) { } }
        private sealed class NoSpeech : ISpeechSender { public void SendSpeech(string text, ushort hue) { } }
        private sealed class NoTarget : ITargetSender { public void SendTarget(in TargetResponse response) { } }
    }
}