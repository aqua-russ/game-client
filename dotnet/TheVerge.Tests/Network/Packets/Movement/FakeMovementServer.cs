using System.Collections.Generic;
using TheVerge.Core.Primitives;
using TheVerge.Network.Packets.Movement;
using TheVerge.Network.Session;

namespace TheVerge.Tests.Network.Packets.Movement
{
    /// <summary>
    /// Фейковый сервер для тестов движения (PLAN Стадия 3, п. 6): модель ModernUO с включённой проверкой темпа для игрока
    /// (Projects/Server/Network/MovementThrottle.cs, ValidateAndQueueMovement, ExecuteMovement, RejectAndReset; без проверки
    /// времени), ответы идут через сеть с задержкой в тиках. Шаг при счёте 0 с номером не 0 — отказ и сброс; поворот (другое
    /// направление без бита бега) — подтверждение без шага (Mobile.Move); подтверждение несёт счётчик сервера до шага
    /// (Mobile.Move: SendMovementAck(Sequence)), после шага счётчик = номер + 1, после 255 — 1. Телепорт и смена карты
    /// сбрасывают счёт (NetState.ResetMovementState) и шлют 0x20 (Mobile.SetLocation, Mobile.InternalMapChange). Запрос
    /// ресинхронизации — 0x20 и сброс счёта (IncomingPlayerPackets.Resynchronize).
    /// </summary>
    internal sealed class FakeMovementServer
    {
        private readonly Queue<Delivery> _toServer = new Queue<Delivery>();
        private readonly Queue<Delivery> _toClient = new Queue<Delivery>();
        private readonly uint _serial;
        private int _sequence;
        private int _requests;

        public FakeMovementServer(uint serial, Point3D position, Direction direction)
        {
            _serial = serial;
            Position = position;
            Direction = direction;
        }

        public Point3D Position { get; private set; }
        public Direction Direction { get; private set; }

        /// <summary>Задержка сети в одну сторону, тиков.</summary>
        public int LatencyTicks { get; set; }

        /// <summary>Каким по счёту запросам шага (с 0) отказать, как будто клетка занята.</summary>
        public HashSet<int> BlockedRequests { get; } = new HashSet<int>();

        /// <summary>
        /// Шаги, пришедшие после сброса с номером не 0, выбрасывать без ответа — так сервер теряет шаги, стоявшие у него в
        /// очереди (ResetMovementState: _movementQueue.Clear).
        /// </summary>
        public bool DropStaleSilently { get; set; }

        /// <summary>Сколько шагов (не поворотов) сервер выполнил.</summary>
        public int Moves { get; private set; }

        public int Rejects { get; private set; }

        /// <summary>Сколько запросов ресинхронизации пришло от клиента.</summary>
        public int Resyncs { get; private set; }

        public bool IsIdle => _toServer.Count == 0 && _toClient.Count == 0;

        public void FromClient(byte[] packet, long tick) => _toServer.Enqueue(new Delivery(tick + LatencyTicks, packet));

        public void Teleport(Point3D position, long tick)
        {
            Position = position;
            _sequence = 0;
            Send(MovementReplyPacketTests.MobileUpdate(new Serial(_serial), position.X, position.Y, position.Z, Direction), tick);
        }

        public void ChangeMap(byte map, Point3D position, long tick)
        {
            Position = position;
            _sequence = 0;
            Send(new byte[] { 0xBF, 0x00, 0x06, 0x00, 0x08, map }, tick);
            Send(MovementReplyPacketTests.MobileUpdate(new Serial(_serial), position.X, position.Y, position.Z, Direction), tick);
        }

        /// <summary>Разобрать дошедшие запросы и отдать клиенту дошедшие ответы.</summary>
        public void Tick(long tick, PacketDispatcher client)
        {
            while (_toServer.Count > 0 && _toServer.Peek().Due <= tick)
            {
                var packet = _toServer.Dequeue().Packet;
                if (packet[0] == MoveRequestPacket.PacketId)
                {
                    OnMoveRequest((Direction)packet[1], packet[2], tick);
                }
                else if (packet[0] == ResyncRequestPacket.PacketId)
                {
                    OnResync(tick);
                }
            }

            while (_toClient.Count > 0 && _toClient.Peek().Due <= tick)
            {
                client.Dispatch(_toClient.Dequeue().Packet);
            }
        }

        private void OnMoveRequest(Direction direction, byte sequence, long tick)
        {
            int index = _requests++;
            if (_sequence == 0 && sequence != 0)
            {
                if (!DropStaleSilently)
                {
                    Reject(sequence, tick);
                }

                return;
            }

            if (BlockedRequests.Contains(index))
            {
                Reject(sequence, tick);
                return;
            }

            if ((direction & Direction.FacingMask) == (Direction & Direction.FacingMask))
            {
                var cell = Position.GetStepPoint(direction & Direction.FacingMask);
                Position = new Point3D(cell.X, cell.Y, Position.Z);
                Moves++;
            }

            Direction = direction;
            Send(new byte[] { 0x22, (byte)_sequence, 0x01 }, tick);
            _sequence = sequence == 255 ? 1 : sequence + 1;
        }

        // Сервер сначала шлёт своё место, затем сбрасывает счёт (0x78 и SendEverything модели не нужны).
        private void OnResync(long tick)
        {
            Resyncs++;
            Send(MovementReplyPacketTests.MobileUpdate(new Serial(_serial), Position.X, Position.Y, Position.Z, Direction), tick);
            _sequence = 0;
        }

        private void Reject(byte sequence, long tick)
        {
            Rejects++;
            var p = Position;
            Send(new byte[] { 0x21, sequence, (byte)(p.X >> 8), (byte)p.X, (byte)(p.Y >> 8), (byte)p.Y, (byte)Direction, (byte)p.Z }, tick);
            _sequence = 0;
        }

        private void Send(byte[] packet, long tick) => _toClient.Enqueue(new Delivery(tick + LatencyTicks, packet));

        private readonly struct Delivery
        {
            public Delivery(long due, byte[] packet)
            {
                Due = due;
                Packet = packet;
            }

            public long Due { get; }
            public byte[] Packet { get; }
        }
    }
}
