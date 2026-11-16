using System;
using System.Collections.Generic;
using System.IO;
using TheVerge.Core.Items;
using TheVerge.Core.Movement;
using TheVerge.Core.Ports;
using TheVerge.Core.Primitives;
using TheVerge.Core.Session;
using TheVerge.Core.Status;
using TheVerge.Core.World;
using TheVerge.Network.Diagnostics;
using TheVerge.Network.Framing;
using TheVerge.Network.IO;
using TheVerge.Network.Packets.Items;
using TheVerge.Network.Packets.Movement;
using TheVerge.Network.Packets.Session;
using TheVerge.Network.Packets.Status;
using TheVerge.Network.Packets.World;
using TheVerge.Network.Session;

namespace TheVerge.Bot
{
    /// <summary>
    /// Проверка записанного лога шагов (<c>--check-walk</c>, PLAN Стадия 3, п. 12 и критерий совместимости с ClassicUO):
    /// прогоняет пакеты мира и запросы шага 0x02 из лога через <see cref="MovementCheck"/> и <see cref="PlayerMotion"/>
    /// на карте из <c>--data</c>, сверяет позиции и Z (0x22, 0x21, 0x20), считает отказы 0x21 и задержку подтверждения.
    /// </summary>
    public static class WalkLogVerifier
    {
        public static int Run(string path, IMapData? map, ITileData? tiles, IMultiData? multis, TextWriter output)
        {
            if (map == null || tiles == null)
            {
                output.WriteLine("ОШИБКА: нужны файлы данных (--data): без карты нельзя проверить шаги и Z.");
                return BotExitCode.BadArguments;
            }

            if (!File.Exists(path))
            {
                output.WriteLine("ОШИБКА: не найден файл лога «" + path + "».");
                return BotExitCode.BadArguments;
            }

            using (var reader = File.OpenText(path))
            {
                return Run(reader, map, tiles, multis, path, output);
            }
        }

        public static int Run(TextReader log, IMapData? map, ITileData? tiles, IMultiData? multis, string sourceName, TextWriter output)
        {
            if (map == null || tiles == null)
            {
                output.WriteLine("ОШИБКА: нужны файлы данных (--data): без карты нельзя проверить шаги и Z.");
                return BotExitCode.BadArguments;
            }

            var session = new ReplayWalkSession(map, tiles, multis);
            try
            {
                var reader = new PacketLogReader(log);
                while (reader.TryRead(out var entry))
                {
                    if (entry.Kind == PacketLogKind.Packet)
                    {
                        session.Feed(entry);
                    }
                }
            }
            catch (PacketLogFormatException e)
            {
                output.WriteLine("ОШИБКА разбора лога «" + sourceName + "»: " + e.Message);
                session.Dispose();
                return BotExitCode.BadArguments;
            }

            int code = session.WriteReport(sourceName, output);
            session.Dispose();
            return code;
        }

        private sealed class ReplayWalkSession : IDisposable
        {
            private readonly ReplayClock _clock = new ReplayClock();
            private readonly EntityStore _store = new EntityStore();
            private readonly LoginProgress _progress = new LoginProgress();
            private readonly PacketDispatcher _dispatcher;
            private readonly WorldEntry _world;
            private readonly PlayerMotion _motion;
            private readonly MovementReplies _replies;
            private readonly MovementStats _stats;
            private readonly HashSet<byte> _blockedSeqs = new HashSet<byte>();
            private readonly List<string> _mismatches = new List<string>();
            private Point3D? _start;
            private bool _inWorld;
            private int _clientSteps;
            private int _stepsSinceServerPlace;
            private int _blockedRejects;

            public ReplayWalkSession(IMapData map, ITileData tiles, IMultiData? multis)
            {
                var silent = new SilentLog();
                _dispatcher = new PacketDispatcher(silent);
                _world = new WorldEntry(_progress, new NullSessionSender());
                var sender = new NullMovementSender();
                _motion = new PlayerMotion(_store, new MovementCheck(map, tiles, multis, _store), sender);
                _replies = new MovementReplies(_motion, sender, silent);
                _stats = new MovementStats(_motion, _replies, _clock);
                _world.PlayerConfirmed += OnPlayerConfirmed;
                _dispatcher.Register(LoginConfirmHandler.PacketId, new LoginConfirmHandler(_world).Handle);
                _dispatcher.Register(LoginCompleteHandler.PacketId, new LoginCompleteHandler(_world).Handle);
                WorldHandlers.Register(_dispatcher, _store, new WorldEnvironment(), _replies);
                MovementHandlers.Register(_dispatcher, _replies);
                StatusHandlers.Register(_dispatcher, new MobileStatusUpdates(_store));
                ItemHandlers.Register(_dispatcher, new ContainerContents(_store));
            }

            public void Feed(PacketLogEntry entry)
            {
                _clock.NowMs = entry.TimeMs;
                var bytes = entry.Packet;
                if (bytes.Length == 0)
                {
                    return;
                }

                if (entry.Direction == PacketDirection.ClientToServer)
                {
                    if (bytes[0] == MoveRequestPacket.PacketId && bytes.Length >= 3)
                    {
                        FeedMoveRequest(entry.TimeMs, bytes[1], bytes[2]);
                    }

                    return;
                }

                FeedServerPacket(entry.TimeMs, bytes);
            }

            public int WriteReport(string sourceName, TextWriter output)
            {
                int totalRejects = _replies.RejectCount + _blockedRejects;
                string ack = _stats.AckCount > 0
                    ? "; подтверждение шага: мин " + _stats.MinAckMs + " мс, среднее " + _stats.AvgAckMs + " мс, макс " + _stats.MaxAckMs + " мс"
                    : string.Empty;
                output.WriteLine("Проверка лога шагов " + sourceName + ": старт " + (_start?.ToString() ?? "—") +
                                 ", итог " + (_motion.IsPlaced ? _motion.Position.ToString() : "—") +
                                 ", шагов " + _clientSteps + ", отказов сервера (0x21) " + totalRejects +
                                 ", расхождений с картой " + _mismatches.Count + ack + ".");
                if (_clientSteps == 0)
                {
                    output.WriteLine("ОШИБКА: в логе нет шагов 0x02.");
                    return BotExitCode.InternalError;
                }

                if (_mismatches.Count > 0)
                {
                    for (int i = 0; i < Math.Min(_mismatches.Count, 10); i++)
                    {
                        output.WriteLine("ОШИБКА: " + _mismatches[i]);
                    }

                    return BotExitCode.InternalError;
                }

                output.WriteLine("ОК: шаги, позиции и Z совпадают с проверкой клиента.");
                return BotExitCode.Success;
            }

            public void Dispose()
            {
                _world.PlayerConfirmed -= OnPlayerConfirmed;
                _stats.Dispose();
            }

            private void OnPlayerConfirmed(PlayerState player)
            {
                _store.Reset(player);
                _motion.EnterWorld(player.Position, player.Direction);
                _start ??= player.Position;
            }

            private void FeedMoveRequest(long ms, byte directionByte, byte sequence)
            {
                if (!_motion.IsPlaced)
                {
                    return;
                }

                var facing = (Direction)(directionByte & 0x07);
                bool run = (directionByte & 0x80) != 0;
                var result = _motion.Step(facing, run);
                if (result == StepResult.Moved)
                {
                    _clientSteps++;
                    _stepsSinceServerPlace++;
                }
                else if (result == StepResult.Blocked)
                {
                    _clientSteps++;
                    _blockedSeqs.Add(sequence);
                }
                else if (result == StepResult.QueueFull)
                {
                    _mismatches.Add("очередь шагов > " + ClientLimits.MaxUnacknowledgedSteps + " на шаге #" + sequence + " (" + ms + " мс)");
                }
            }

            private void FeedServerPacket(long ms, byte[] bytes)
            {
                byte id = bytes[0];
                if (id == LoginConfirmHandler.PacketId)
                {
                    _progress.MoveTo(LoginState.EnteringWorld);
                }
                else if (id == LoginCompleteHandler.PacketId)
                {
                    _inWorld = true;
                }
                else if (id == MoveAckHandler.PacketId && bytes.Length >= 2 && _blockedSeqs.Remove(bytes[1]))
                {
                    _mismatches.Add("шаг #" + bytes[1] + " (" + ms + " мс): сервер принял (0x22), но MovementCheck клиента не пустил в " + _motion.Position);
                    return;
                }
                else if (id == MoveRejectHandler.PacketId && bytes.Length >= 8 && _blockedSeqs.Remove(bytes[1]))
                {
                    ResetFromReject(bytes);
                    return;
                }

                bool checkUpdate = id == MobileUpdateHandler.PacketId && _inWorld && _motion.IsPlaced &&
                                   _motion.PendingSteps == 0 && _stepsSinceServerPlace > 0;
                var before = _motion.Position;
                if (PacketTable.Get(PacketDirection.ServerToClient, id).IsKnown)
                {
                    _dispatcher.Dispatch(bytes);
                }

                if (id == MobileUpdateHandler.PacketId)
                {
                    _stepsSinceServerPlace = 0;
                }

                if (checkUpdate && _store.Player != null)
                {
                    int dist = Math.Max(Math.Abs(_store.Player.Position.X - before.X), Math.Abs(_store.Player.Position.Y - before.Y));
                    if (dist <= ClientLimits.StepSnapGapCells && _store.Player.Position != before)
                    {
                        _mismatches.Add("в " + ms + " мс сервер 0x20 ставит в " + _store.Player.Position + ", а клиент предсказал " + before);
                    }
                }
            }

            // Повторяет раскладку MoveRejectHandler, если шаг 0x02 ранее не прошёл локальную проверку и не попал в очередь.
            private void ResetFromReject(byte[] bytes)
            {
                var reader = new PacketReader(bytes.AsSpan(1));
                reader.Skip(1);
                var pos = new Point3D(reader.ReadInt16(), reader.ReadInt16(), 0);
                var dir = (Direction)reader.ReadByte();
                pos = new Point3D(pos.X, pos.Y, reader.ReadSByte());
                _motion.ResetToServer(pos, dir);
                _blockedRejects++;
            }

            private sealed class ReplayClock : IClock { public long NowMs { get; set; } }
            private sealed class SilentLog : ILog { public void Info(string m) { } public void Warning(string m) { } public void Error(string m) { } }
            private sealed class NullSessionSender : ISessionSender { public void SendClientVersion(ClientVersion v) { } public void SendLanguage(string l) { } public void SendViewRange(int r) { } public void SendPing(byte s) { } public void SendLogout() { } }
            private sealed class NullMovementSender : IMovementSender { public void SendStep(Direction d, bool r, byte s) { } public void SendResync() { } }
        }
    }
}